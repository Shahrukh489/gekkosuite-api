using System.Security.Claims;

namespace GekkoSuite.Api.Authentication;

/// <summary>
/// Reads the request context (userId + organizationId) a validated JWT puts onto ClaimsPrincipal. These
/// are the only two things the token carries — auth.md deliberately keeps roles/permissions out of it so
/// access changes take effect immediately instead of waiting for the token to expire.
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>
    /// Reads the authenticated user's id from the token's claims.
    /// </summary>
    /// <param name="principal">The current request's authenticated principal.</param>
    /// <returns>The user id, or null if missing/unparseable (an unauthenticated request, or a malformed token).</returns>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("userId");
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    /// <summary>
    /// Reads the authenticated user's organization id from the token's claims — the tenant boundary
    /// every request is checked against.
    /// </summary>
    /// <param name="principal">The current request's authenticated principal.</param>
    /// <returns>The organization id, or null if missing/unparseable.</returns>
    public static Guid? GetOrganizationId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("organizationId");
        return Guid.TryParse(value, out var organizationId) ? organizationId : null;
    }
}
