using IdentityServerHost.Quickstart.UI;

namespace eShop.Identity.API.Services;

/// <summary>
/// Builds Quickstart account view models from IdentityServer interaction state.
/// </summary>
public interface IAccountViewModelService
{
    /// <summary>
    /// Creates the login view model for the supplied return URL.
    /// </summary>
    Task<LoginViewModel> BuildLoginViewModelAsync(string returnUrl);

    /// <summary>
    /// Creates the login view model for a posted login form.
    /// </summary>
    Task<LoginViewModel> BuildLoginViewModelAsync(LoginInputModel model);

    /// <summary>
    /// Creates the logout confirmation view model for the current user.
    /// </summary>
    Task<LogoutViewModel> BuildLogoutViewModelAsync(string logoutId, ClaimsPrincipal user);

    /// <summary>
    /// Creates the logged-out view model for the current user and request.
    /// </summary>
    Task<LoggedOutViewModel> BuildLoggedOutViewModelAsync(string logoutId, ClaimsPrincipal user, HttpContext httpContext);
}

/// <summary>
/// Centralizes account Quickstart view-model composition away from MVC request handling.
/// </summary>
public sealed class AccountViewModelService : IAccountViewModelService
{
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IClientStore _clientStore;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IAuthenticationHandlerProvider _handlerProvider;

    public AccountViewModelService(
        IIdentityServerInteractionService interaction,
        IClientStore clientStore,
        IAuthenticationSchemeProvider schemeProvider,
        IAuthenticationHandlerProvider handlerProvider)
    {
        _interaction = interaction;
        _clientStore = clientStore;
        _schemeProvider = schemeProvider;
        _handlerProvider = handlerProvider;
    }

    public async Task<LoginViewModel> BuildLoginViewModelAsync(string returnUrl)
    {
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        var externalOnlyLogin = await TryBuildExternalOnlyLoginViewModelAsync(context, returnUrl);
        if (externalOnlyLogin is not null)
        {
            return externalOnlyLogin;
        }

        var providers = await GetVisibleProvidersAsync();
        var allowLocalLogin = await ApplyClientRestrictionsAsync(context, providers);

        return new LoginViewModel
        {
            AllowRememberLogin = AccountOptions.AllowRememberLogin,
            EnableLocalLogin = allowLocalLogin && AccountOptions.AllowLocalLogin,
            ReturnUrl = returnUrl,
            Username = context?.LoginHint,
                ExternalProviders = providers.ToArray(),
                VisibleExternalProviders = providers.Where(provider => !string.IsNullOrWhiteSpace(provider.DisplayName)).ToArray(),
                IsExternalLoginOnly = !allowLocalLogin && providers.Count == 1,
                ExternalLoginScheme = !allowLocalLogin && providers.Count == 1 ? providers[0].AuthenticationScheme : null
        };
    }

    public async Task<LoginViewModel> BuildLoginViewModelAsync(LoginInputModel model)
    {
        var viewModel = await BuildLoginViewModelAsync(model.ReturnUrl);
        viewModel.Username = model.Username;
        viewModel.RememberLogin = model.RememberLogin;
        return viewModel;
    }

    public async Task<LogoutViewModel> BuildLogoutViewModelAsync(string logoutId, ClaimsPrincipal user)
    {
        var viewModel = new LogoutViewModel
        {
            LogoutId = logoutId,
            ShowLogoutPrompt = AccountOptions.ShowLogoutPrompt
        };

        if (user?.Identity?.IsAuthenticated != true)
        {
            viewModel.ShowLogoutPrompt = false;
            return viewModel;
        }

        var context = await _interaction.GetLogoutContextAsync(logoutId);
        if (context?.ShowSignoutPrompt == false)
        {
            viewModel.ShowLogoutPrompt = false;
        }

        return viewModel;
    }

    public async Task<LoggedOutViewModel> BuildLoggedOutViewModelAsync(string logoutId, ClaimsPrincipal user, HttpContext httpContext)
    {
        var logoutContext = await _interaction.GetLogoutContextAsync(logoutId);
        var viewModel = new LoggedOutViewModel
        {
            AutomaticRedirectAfterSignOut = AccountOptions.AutomaticRedirectAfterSignOut,
            PostLogoutRedirectUri = logoutContext?.PostLogoutRedirectUri,
            ClientName = string.IsNullOrEmpty(logoutContext?.ClientName) ? logoutContext?.ClientId : logoutContext?.ClientName,
            SignOutIframeUrl = logoutContext?.SignOutIFrameUrl,
            LogoutId = logoutId
        };

        await TryConfigureExternalSignoutAsync(viewModel, user, httpContext);
        return viewModel;
    }

    private async Task<LoginViewModel> TryBuildExternalOnlyLoginViewModelAsync(AuthorizationRequest context, string returnUrl)
    {
        if (context?.IdP is null || await _schemeProvider.GetSchemeAsync(context.IdP) is null)
        {
            return null;
        }

        var isLocalLogin = context.IdP == IdentityServerConstants.LocalIdentityProvider;
        var viewModel = new LoginViewModel
        {
            EnableLocalLogin = isLocalLogin,
            ReturnUrl = returnUrl,
            Username = context.LoginHint,
        };

        if (!isLocalLogin)
        {
                ConfigureExternalProviders(viewModel, new List<ExternalProvider> { new ExternalProvider { AuthenticationScheme = context.IdP } });
        }

        return viewModel;
    }

        private static void ConfigureExternalProviders(LoginViewModel viewModel, IReadOnlyList<ExternalProvider> providers)
        {
            viewModel.ExternalProviders = providers;
            viewModel.VisibleExternalProviders = providers.Where(provider => !string.IsNullOrWhiteSpace(provider.DisplayName)).ToArray();
            viewModel.IsExternalLoginOnly = !viewModel.EnableLocalLogin && providers.Count == 1;
            viewModel.ExternalLoginScheme = viewModel.IsExternalLoginOnly ? providers[0].AuthenticationScheme : null;
        }

    private async Task<List<ExternalProvider>> GetVisibleProvidersAsync()
    {
        var schemes = await _schemeProvider.GetAllSchemesAsync();
        return schemes
            .Where(scheme => scheme.DisplayName is not null)
            .Select(scheme => new ExternalProvider
            {
                DisplayName = scheme.DisplayName ?? scheme.Name,
                AuthenticationScheme = scheme.Name
            })
            .ToList();
    }

    private async Task<bool> ApplyClientRestrictionsAsync(AuthorizationRequest context, List<ExternalProvider> providers)
    {
        if (context?.Client.ClientId is null)
        {
            return true;
        }

        var client = await _clientStore.FindEnabledClientByIdAsync(context.Client.ClientId);
        if (client is null)
        {
            return true;
        }

        if (client.IdentityProviderRestrictions is { Count: > 0 })
        {
            providers.RemoveAll(provider => !client.IdentityProviderRestrictions.Contains(provider.AuthenticationScheme));
        }

        return client.EnableLocalLogin;
    }

    private async Task TryConfigureExternalSignoutAsync(LoggedOutViewModel viewModel, ClaimsPrincipal user, HttpContext httpContext)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var identityProvider = user.FindFirst(JwtClaimTypes.IdentityProvider)?.Value;
        if (identityProvider is null || identityProvider == IdentityServerConstants.LocalIdentityProvider)
        {
            return;
        }

        var handler = await _handlerProvider.GetHandlerAsync(httpContext, identityProvider);
        if (handler is not IAuthenticationSignOutHandler)
        {
            return;
        }

        viewModel.LogoutId ??= await _interaction.CreateLogoutContextAsync();
        viewModel.ExternalAuthenticationScheme = identityProvider;
    }
}