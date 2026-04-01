using IdentityServerHost.Quickstart.UI;

namespace eShop.Identity.API.Services;

/// <summary>
/// Encapsulates consent processing and view-model creation for the Quickstart UI.
/// </summary>
public interface IConsentWorkflowService
{
    /// <summary>
    /// Builds the consent view model for the supplied return URL.
    /// </summary>
    Task<ConsentViewModel> BuildViewModelAsync(string returnUrl, ConsentInputModel model = null);

    /// <summary>
    /// Processes the posted consent form for the current user.
    /// </summary>
    Task<ProcessConsentResult> ProcessConsentAsync(ConsentInputModel model, ClaimsPrincipal user);
}

/// <summary>
/// Implements the consent workflow for the Quickstart UI.
/// </summary>
public sealed class ConsentWorkflowService : IConsentWorkflowService
{
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;
    private readonly ILogger<ConsentWorkflowService> _logger;

    public ConsentWorkflowService(
        IIdentityServerInteractionService interaction,
        IEventService events,
        ILogger<ConsentWorkflowService> logger)
    {
        _interaction = interaction;
        _events = events;
        _logger = logger;
    }

    public async Task<ConsentViewModel> BuildViewModelAsync(string returnUrl, ConsentInputModel model = null)
    {
        var request = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (request is null)
        {
            _logger.LogError("No consent request matching request: {ReturnUrl}", returnUrl);
            return null;
        }

        return CreateConsentViewModel(model, returnUrl, request);
    }

    public async Task<ProcessConsentResult> ProcessConsentAsync(ConsentInputModel model, ClaimsPrincipal user)
    {
        var result = new ProcessConsentResult();
        var request = await _interaction.GetAuthorizationContextAsync(model.ReturnUrl);
        if (request is null)
        {
            return result;
        }

        var grantedConsent = await CreateConsentResponseAsync(model, request, user, result);
        if (grantedConsent is not null)
        {
            await _interaction.GrantConsentAsync(request, grantedConsent);
            result.RedirectUri = model.ReturnUrl;
            result.Client = request.Client;
            return result;
        }

        result.ViewModel = await BuildViewModelAsync(model.ReturnUrl, model);
        return result;
    }

    private async Task<ConsentResponse> CreateConsentResponseAsync(
        ConsentInputModel model,
        AuthorizationRequest request,
        ClaimsPrincipal user,
        ProcessConsentResult result)
    {
        if (model?.Button == "no")
        {
            await _events.RaiseAsync(new ConsentDeniedEvent(user.GetSubjectId(), request.Client.ClientId, request.ValidatedResources.RawScopeValues));
            return new ConsentResponse { Error = AuthorizationError.AccessDenied };
        }

        if (model?.Button != "yes")
        {
            result.ValidationError = ConsentOptions.InvalidSelectionErrorMessage;
            return null;
        }

        var scopes = GetConsentedScopes(model);
        if (!scopes.Any())
        {
            result.ValidationError = ConsentOptions.MustChooseOneErrorMessage;
            return null;
        }

        var grantedConsent = new ConsentResponse
        {
            RememberConsent = model.RememberConsent,
            ScopesValuesConsented = scopes.ToArray(),
            Description = model.Description
        };

        await _events.RaiseAsync(new ConsentGrantedEvent(
            user.GetSubjectId(),
            request.Client.ClientId,
            request.ValidatedResources.RawScopeValues,
            grantedConsent.ScopesValuesConsented,
            grantedConsent.RememberConsent));

        return grantedConsent;
    }

    private static IEnumerable<string> GetConsentedScopes(ConsentInputModel model)
    {
        if (model.ScopesConsented is null)
        {
            return Enumerable.Empty<string>();
        }

        return ConsentOptions.EnableOfflineAccess
            ? model.ScopesConsented
            : model.ScopesConsented.Where(scope => scope != IdentityServerConstants.StandardScopes.OfflineAccess);
    }

    private static ConsentViewModel CreateConsentViewModel(ConsentInputModel model, string returnUrl, AuthorizationRequest request)
    {
        var viewModel = new ConsentViewModel
        {
            RememberConsent = model?.RememberConsent ?? true,
            ScopesConsented = model?.ScopesConsented ?? Enumerable.Empty<string>(),
            Description = model?.Description,
            ReturnUrl = returnUrl,
            ClientName = request.Client.ClientName ?? request.Client.ClientId,
            ClientUrl = request.Client.ClientUri,
            ClientLogoUrl = request.Client.LogoUri,
            AllowRememberConsent = request.Client.AllowRememberConsent
        };

        viewModel.IdentityScopes = request.ValidatedResources.Resources.IdentityResources
            .Select(identity => CreateIdentityScopeViewModel(identity, viewModel.ScopesConsented.Contains(identity.Name) || model is null))
            .ToArray();

        viewModel.ApiScopes = CreateApiScopes(model, request, viewModel.ScopesConsented).ToArray();
        return viewModel;
    }

    private static IEnumerable<ScopeViewModel> CreateApiScopes(ConsentInputModel model, AuthorizationRequest request, IEnumerable<string> scopesConsented)
    {
        var apiScopes = request.ValidatedResources.ParsedScopes
            .Select(parsedScope => CreateApiScopeViewModel(request, parsedScope, scopesConsented, model is null))
            .Where(scope => scope is not null)
            .ToList();

        if (ConsentOptions.EnableOfflineAccess && request.ValidatedResources.Resources.OfflineAccess)
        {
            apiScopes.Add(CreateOfflineAccessScope(scopesConsented.Contains(IdentityServerConstants.StandardScopes.OfflineAccess) || model is null));
        }

        return apiScopes;
    }

    private static ScopeViewModel CreateIdentityScopeViewModel(IdentityResource identity, bool isChecked)
    {
        return new ScopeViewModel
        {
            Value = identity.Name,
            DisplayName = identity.DisplayName ?? identity.Name,
            Description = identity.Description,
            Emphasize = identity.Emphasize,
            Required = identity.Required,
            Checked = isChecked || identity.Required
        };
    }

    private static ScopeViewModel CreateApiScopeViewModel(
        AuthorizationRequest request,
        ParsedScopeValue parsedScope,
        IEnumerable<string> scopesConsented,
        bool defaultChecked)
    {
        var apiScope = request.ValidatedResources.Resources.FindApiScope(parsedScope.ParsedName);
        if (apiScope is null)
        {
            return null;
        }

        var displayName = apiScope.DisplayName ?? apiScope.Name;
        if (!string.IsNullOrWhiteSpace(parsedScope.ParsedParameter))
        {
            displayName += ":" + parsedScope.ParsedParameter;
        }

        return new ScopeViewModel
        {
            Value = parsedScope.RawValue,
            DisplayName = displayName,
            Description = apiScope.Description,
            Emphasize = apiScope.Emphasize,
            Required = apiScope.Required,
            Checked = scopesConsented.Contains(parsedScope.RawValue) || defaultChecked || apiScope.Required
        };
    }

    private static ScopeViewModel CreateOfflineAccessScope(bool isChecked)
    {
        return new ScopeViewModel
        {
            Value = IdentityServerConstants.StandardScopes.OfflineAccess,
            DisplayName = ConsentOptions.OfflineAccessDisplayName,
            Description = ConsentOptions.OfflineAccessDescription,
            Emphasize = true,
            Checked = isChecked
        };
    }
}