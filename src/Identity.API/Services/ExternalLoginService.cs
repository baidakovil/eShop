namespace eShop.Identity.API.Services;

/// <summary>
/// Encapsulates external login lookup, provisioning, and callback state handling.
/// </summary>
public interface IExternalLoginService
{
    /// <summary>
    /// Resolves the local user and provider data from an external authentication result.
    /// </summary>
    Task<(ApplicationUser User, string Provider, string ProviderUserId, IEnumerable<Claim> Claims)> FindUserFromExternalProviderAsync(AuthenticateResult result);

    /// <summary>
    /// Creates a local user for a previously unseen external identity.
    /// </summary>
    Task<ApplicationUser> AutoProvisionUserAsync(string provider, string providerUserId, IEnumerable<Claim> claims);

    /// <summary>
    /// Preserves external protocol values needed for the local sign-in cookie.
    /// </summary>
    ExternalLoginCallbackDetails CreateLoginCallbackDetails(AuthenticateResult externalResult);
}

/// <summary>
/// Stores local claims and authentication properties derived from an external login callback.
/// </summary>
public sealed class ExternalLoginCallbackDetails
{
    /// <summary>
    /// Gets the claims to persist into the local authentication session.
    /// </summary>
    public List<Claim> LocalClaims { get; } = [];

    /// <summary>
    /// Gets the authentication properties to persist into the local authentication session.
    /// </summary>
    public AuthenticationProperties SignInProperties { get; } = new();
}

/// <summary>
/// Implements the external login workflow for the Identity API.
/// </summary>
public sealed class ExternalLoginService : IExternalLoginService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ExternalLoginService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<(ApplicationUser User, string Provider, string ProviderUserId, IEnumerable<Claim> Claims)> FindUserFromExternalProviderAsync(AuthenticateResult result)
    {
        var externalUser = result.Principal;
        var userIdClaim = externalUser.FindFirst(JwtClaimTypes.Subject)
            ?? externalUser.FindFirst(ClaimTypes.NameIdentifier)
            ?? throw new Exception("Unknown userid");

        var claims = externalUser.Claims.ToList();
        claims.Remove(userIdClaim);

        var provider = result.Properties.Items["scheme"];
        var providerUserId = userIdClaim.Value;
        var user = await _userManager.FindByLoginAsync(provider, providerUserId);

        return (user, provider, providerUserId, claims);
    }

    public async Task<ApplicationUser> AutoProvisionUserAsync(string provider, string providerUserId, IEnumerable<Claim> claims)
    {
        var filteredClaims = CreateProvisioningClaims(claims);
        var user = new ApplicationUser
        {
            UserName = Guid.NewGuid().ToString(),
        };

        await CreateUserAsync(user);
        await AddClaimsAsync(user, filteredClaims);
        await AddLoginAsync(user, provider, providerUserId);

        return user;
    }

    public ExternalLoginCallbackDetails CreateLoginCallbackDetails(AuthenticateResult externalResult)
    {
        var details = new ExternalLoginCallbackDetails();
        var sessionId = externalResult.Principal.Claims.FirstOrDefault(claim => claim.Type == JwtClaimTypes.SessionId);
        if (sessionId is not null)
        {
            details.LocalClaims.Add(new Claim(JwtClaimTypes.SessionId, sessionId.Value));
        }

        var idToken = externalResult.Properties.GetTokenValue("id_token");
        if (idToken is not null)
        {
            details.SignInProperties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
        }

        return details;
    }

    private static List<Claim> CreateProvisioningClaims(IEnumerable<Claim> claims)
    {
        var filteredClaims = new List<Claim>();
        var name = claims.FirstOrDefault(claim => claim.Type == JwtClaimTypes.Name)?.Value
            ?? claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Name)?.Value;

        AddDisplayNameClaim(filteredClaims, claims, name);

        var email = claims.FirstOrDefault(claim => claim.Type == JwtClaimTypes.Email)?.Value
            ?? claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Email)?.Value;
        if (email is not null)
        {
            filteredClaims.Add(new Claim(JwtClaimTypes.Email, email));
        }

        return filteredClaims;
    }

    private static void AddDisplayNameClaim(List<Claim> filteredClaims, IEnumerable<Claim> claims, string name)
    {
        if (name is not null)
        {
            filteredClaims.Add(new Claim(JwtClaimTypes.Name, name));
            return;
        }

        var firstName = claims.FirstOrDefault(claim => claim.Type == JwtClaimTypes.GivenName)?.Value
            ?? claims.FirstOrDefault(claim => claim.Type == ClaimTypes.GivenName)?.Value;
        var lastName = claims.FirstOrDefault(claim => claim.Type == JwtClaimTypes.FamilyName)?.Value
            ?? claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Surname)?.Value;
        var fullName = (firstName, lastName) switch
        {
            (not null, not null) => $"{firstName} {lastName}",
            (not null, null) => firstName,
            (null, not null) => lastName,
            _ => null,
        };

        if (fullName is not null)
        {
            filteredClaims.Add(new Claim(JwtClaimTypes.Name, fullName));
        }
    }

    private async Task CreateUserAsync(ApplicationUser user)
    {
        var identityResult = await _userManager.CreateAsync(user);
        EnsureSuccess(identityResult);
    }

    private async Task AddClaimsAsync(ApplicationUser user, List<Claim> filteredClaims)
    {
        if (!filteredClaims.Any())
        {
            return;
        }

        var identityResult = await _userManager.AddClaimsAsync(user, filteredClaims);
        EnsureSuccess(identityResult);
    }

    private async Task AddLoginAsync(ApplicationUser user, string provider, string providerUserId)
    {
        var identityResult = await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerUserId, provider));
        EnsureSuccess(identityResult);
    }

    private static void EnsureSuccess(IdentityResult identityResult)
    {
        if (!identityResult.Succeeded)
        {
            throw new Exception(identityResult.Errors.First().Description);
        }
    }
}