using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;

using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Policies;

/// <summary>
/// Enriches the authenticated principal with the caller's organizationId. 
/// The token carries only userId, so the organizationId is resolved from the DB per request
/// and added as a claim 
/// Note: this only enriches — it cannot reject;
/// The AuthenticationMiddleware runs after this to verify and reject 
/// </summary>
public class OrganizationClaimsTransformation : IClaimsTransformation
{
    private readonly IUserService _userService;
    private readonly ILogger<OrganizationClaimsTransformation> _logger;

    public OrganizationClaimsTransformation(IUserService userService, ILogger<OrganizationClaimsTransformation> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Adds an organizationId claim resolved from the principal's userId.
    /// </summary>
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // @TODO: consider carrying organizationId in the JWT itself and reading it from there instead of a
        // per-request DB lookup. Tradeoff: cheaper (no query per request) but the token would need re-issuing
        // to reflect an org change, and we lose the immediate-revocation property of resolving it live.
        var userId = principal.FindFirst("userId")?.Value;
        if (!Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogDebug("Claims transform: no valid userId claim on the token, cant get organizationId.");
            return principal;
        }

        _logger.LogDebug("Claims transform: resolving organization for user {UserId}.", userGuid);

        Guid? organizationId = await _userService.GetUserOrganizationIdAsync(userGuid);
        if (organizationId is null)
        {
            _logger.LogDebug("Claims transform: user {UserId} resolved to no organizationId.", userGuid);
            // @TODO: verify if we can throw an exception here instead.
            return principal;
        }

        if (principal.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim("organizationId", organizationId.Value.ToString()));
            _logger.LogDebug("Claims transform: added organizationId {OrganizationId} for user {UserId}.", organizationId, userGuid);
        }

        return principal;
    }
}
