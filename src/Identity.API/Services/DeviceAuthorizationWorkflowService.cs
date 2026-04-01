using IdentityServerHost.Quickstart.UI;

namespace eShop.Identity.API.Services;

/// <summary>
/// Encapsulates device authorization processing and view-model creation for the Quickstart UI.
/// </summary>
public interface IDeviceAuthorizationWorkflowService
{
    /// <summary>
    /// Builds the device authorization view model for the supplied user code.
    /// </summary>
    Task<DeviceAuthorizationViewModel> BuildViewModelAsync(string userCode, DeviceAuthorizationInputModel model = null);

    /// <summary>
    /// Processes the posted device authorization form for the current user.
    /// </summary>
    Task<ProcessConsentResult> ProcessConsentAsync(DeviceAuthorizationInputModel model, ClaimsPrincipal user);
}

/// <summary>
/// Implements the device authorization workflow for the Quickstart UI.
/// </summary>
public sealed class DeviceAuthorizationWorkflowService : IDeviceAuthorizationWorkflowService
{
    private readonly IDeviceFlowInteractionService _interaction;
    private readonly IEventService _events;

    public DeviceAuthorizationWorkflowService(
        IDeviceFlowInteractionService interaction,
        IEventService events)
    {
        _interaction = interaction;
        _events = events;
    }

    public async Task<DeviceAuthorizationViewModel> BuildViewModelAsync(string userCode, DeviceAuthorizationInputModel model = null)
    {
        var request = await _interaction.GetAuthorizationContextAsync(userCode);
        return request is null ? null : CreateConsentViewModel(userCode, model, request);
    }

    public async Task<ProcessConsentResult> ProcessConsentAsync(DeviceAuthorizationInputModel model, ClaimsPrincipal user)
    {
        var result = new ProcessConsentResult();
        var request = await _interaction.GetAuthorizationContextAsync(model.UserCode);
        if (request is null)
        {
            return result;
        }

        var grantedConsent = await CreateConsentResponseAsync(model, request, user, result);
        if (grantedConsent is not null)
        {
            await _interaction.HandleRequestAsync(model.UserCode, grantedConsent);
            result.RedirectUri = model.ReturnUrl;
            result.Client = request.Client;
            return result;
        }

        result.ViewModel = await BuildViewModelAsync(model.UserCode, model);
        return result;
    }

    private async Task<ConsentResponse> CreateConsentResponseAsync(
        DeviceAuthorizationInputModel model,
        DeviceFlowAuthorizationRequest request,
        ClaimsPrincipal user,
        ProcessConsentResult result)
    {
        if (model.Button == "no")
        {
            await _events.RaiseAsync(new ConsentDeniedEvent(user.GetSubjectId(), request.Client.ClientId, request.ValidatedResources.RawScopeValues));
            return new ConsentResponse { Error = AuthorizationError.AccessDenied };
        }

        if (model.Button != "yes")
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

    private static IEnumerable<string> GetConsentedScopes(DeviceAuthorizationInputModel model)
    {
        if (model.ScopesConsented is null)
        {
            return Enumerable.Empty<string>();
        }

        return ConsentOptions.EnableOfflineAccess
            ? model.ScopesConsented
            : model.ScopesConsented.Where(scope => scope != IdentityServerConstants.StandardScopes.OfflineAccess);
    }

    private static DeviceAuthorizationViewModel CreateConsentViewModel(
        string userCode,
        DeviceAuthorizationInputModel model,
        DeviceFlowAuthorizationRequest request)
    {
        var viewModel = new DeviceAuthorizationViewModel
        {
            UserCode = userCode,
            Description = model?.Description,
            RememberConsent = model?.RememberConsent ?? true,
            ScopesConsented = model?.ScopesConsented ?? Enumerable.Empty<string>(),
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

    private static IEnumerable<ScopeViewModel> CreateApiScopes(
        DeviceAuthorizationInputModel model,
        DeviceFlowAuthorizationRequest request,
        IEnumerable<string> scopesConsented)
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
        DeviceFlowAuthorizationRequest request,
        ParsedScopeValue parsedScope,
        IEnumerable<string> scopesConsented,
        bool defaultChecked)
    {
        var apiScope = request.ValidatedResources.Resources.FindApiScope(parsedScope.ParsedName);
        if (apiScope is null)
        {
            return null;
        }

        return new ScopeViewModel
        {
            Value = parsedScope.RawValue,
            DisplayName = apiScope.DisplayName ?? apiScope.Name,
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