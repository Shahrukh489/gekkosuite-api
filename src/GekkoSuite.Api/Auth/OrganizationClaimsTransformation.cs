using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;

using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Auth;

/// <summary>
/// Enriches the authenticated principal with the caller's organizationId. The token carries only userId, so
/// the org is resolved from the DB per request and added as a claim (never re-signed into the token). This
/// only enriches — it cannot reject; the AuthenticationMiddleware gates on the claim's presence.
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
    /// Adds an organizationId claim resolved from the principal's userId. Assumes a single JWT scheme with no
    /// manual re-authentication, so it runs once per request on a fresh principal (no idempotency guard).
    /// </summary>
    /// <param name="principal">The authenticated principal carrying the userId claim.</param>
    /// <returns>The same principal, with an organizationId claim added when one could be resolved.</returns>
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirst("userId")?.Value;
        if (!Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogDebug("Claims transform: no valid userId claim on the token; adding no organizationId.");
            return principal;
        }

        _logger.LogDebug("Claims transform: resolving organization for user {UserId}.", userGuid);

        Guid? organizationId = await _userService.GetUserOrganizationIdAsync(userGuid);
        if (organizationId is null)
        {
            _logger.LogDebug("Claims transform: user {UserId} resolved to no live, active organization; adding no claim.", userGuid);
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
