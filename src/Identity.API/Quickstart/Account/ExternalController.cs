namespace IdentityServerHost.Quickstart.UI;

[SecurityHeaders]
[AllowAnonymous]
public class ExternalController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IExternalLoginService _externalLoginService;
    private readonly IEventService _events;
    private readonly ILogger<ExternalController> _logger;

    public ExternalController(
        SignInManager<ApplicationUser> signInManager,
        IIdentityServerInteractionService interaction,
        IExternalLoginService externalLoginService,
        IEventService events,
        ILogger<ExternalController> logger)
    {
        _signInManager = signInManager;
        _interaction = interaction;
        _externalLoginService = externalLoginService;
        _events = events;
        _logger = logger;
    }

    /// <summary>
    /// initiate roundtrip to external authentication provider
    /// </summary>
    [HttpGet]
    public IActionResult Challenge(string scheme, string returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl)) returnUrl = "~/";

        // validate returnUrl - either it is a valid OIDC URL or back to a local page
        if (Url.IsLocalUrl(returnUrl) == false && _interaction.IsValidReturnUrl(returnUrl) == false)
        {
            // user might have clicked on a malicious link - should be logged
            throw new Exception("invalid return URL");
        }

        // start challenge and roundtrip the return URL and scheme 
        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(Callback)),
            Items =
                {
                    { "returnUrl", returnUrl },
                    { "scheme", scheme },
                }
        };

        return Challenge(props, scheme);

    }

    /// <summary>
    /// Post processing of external authentication
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Callback()
    {
        // read external identity from the temporary cookie
        var result = await HttpContext.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
        if (result?.Succeeded != true)
        {
            throw new Exception("External authentication error");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var externalClaims = result.Principal.Claims.Select(c => $"{c.Type}: {c.Value}");
            _logger.LogDebug("External claims: {@claims}", externalClaims);
        }

        // lookup our user and external provider info
        var (user, provider, providerUserId, claims) = await _externalLoginService.FindUserFromExternalProviderAsync(result);
        if (user == null)
        {
            // this might be where you might initiate a custom workflow for user registration
            // in this sample we don't show how that would be done, as our sample implementation
            // simply auto-provisions new external user
            user = await _externalLoginService.AutoProvisionUserAsync(provider, providerUserId, claims);
        }

        // this allows us to collect any additional claims or properties
        // for the specific protocols used and store them in the local auth cookie.
        // this is typically used to store data needed for signout from those protocols.
        var callbackDetails = _externalLoginService.CreateLoginCallbackDetails(result);

        // issue authentication cookie for user
        // we must issue the cookie maually, and can't use the SignInManager because
        // it doesn't expose an API to issue additional claims from the login workflow
        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        callbackDetails.LocalClaims.AddRange(principal.Claims);
        var name = principal.FindFirst(JwtClaimTypes.Name)?.Value ?? user.Id;

        var isuser = new IdentityServerUser(user.Id)
        {
            DisplayName = name,
            IdentityProvider = provider,
            AdditionalClaims = callbackDetails.LocalClaims
        };

        await HttpContext.SignInAsync(isuser, callbackDetails.SignInProperties);

        // delete temporary cookie used during external authentication
        await HttpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

        // retrieve return URL
        var returnUrl = result.Properties.Items["returnUrl"] ?? "~/";

        // check if external login is in the context of an OIDC request
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        await _events.RaiseAsync(new UserLoginSuccessEvent(provider, providerUserId, user.Id, name, true, context?.Client.ClientId));

        return this.RedirectToReturnUrl(returnUrl, context);
    }
}
