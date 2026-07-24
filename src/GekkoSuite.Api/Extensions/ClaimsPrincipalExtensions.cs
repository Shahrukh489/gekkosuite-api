using System.Security.Claims;

namespace GekkoSuite.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the userId claim from the validated token. Throws if it is missing or not a valid GUID.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        return GetRequiredGuidClaim(user, "userId");
    }

    /// <summary>
    /// Reads the organizationId claim from the validated token. Throws if it is missing or not a valid GUID.
    /// </summary>
    public static Guid GetOrganizationId(this ClaimsPrincipal user)
    {
        return GetRequiredGuidClaim(user, "organizationId");
    }

    /// <summary>
    /// Reads a required GUID claim, throwing a clear exception if it is missing or malformed. A failure
    /// here means the token validation was misconfigured or bypassed, so we throw rather than fake a value.
    /// </summary>
    private static Guid GetRequiredGuidClaim(ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirst(claimType)?.Value;
        if (!Guid.TryParse(value, out var id))
        {
            throw new InvalidOperationException($"Missing or invalid '{claimType}' claim on the token.");
        }

        return id;
    }
}
