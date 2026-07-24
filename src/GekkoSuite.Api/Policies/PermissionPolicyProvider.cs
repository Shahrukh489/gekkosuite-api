using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Policies;

/// <summary>
/// Builds authorization policies for HasPermissionAttribute at request time: it parses the scope and
/// optional permission encoded in the policy name into a PermissionRequirement, so no policy has to be
/// pre-registered per permission. Any other policy name falls through to the default provider.
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    /// <summary>
    /// Returns the policy for a name. Names starting with the HasPermission prefix are parsed into a
    /// PermissionRequirement; everything else defers to the default provider.
    /// </summary>
    /// <param name="policyName">The policy name from the endpoint's attribute.</param>
    /// <returns>The built policy, or the default provider's result.</returns>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.PolicyPrefix))
        {
            return _fallbackProvider.GetPolicyAsync(policyName);
        }

        // strip the prefix, then split into scope and (optional) permission at the first colon
        string body = policyName.Substring(HasPermissionAttribute.PolicyPrefix.Length);
        int separator = body.IndexOf(':');

        MembershipScope scope = Enum.Parse<MembershipScope>(body.Substring(0, separator));
        string permissionText = body.Substring(separator + 1);
        string? permission = permissionText.Length == 0 ? null : permissionText;

        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(scope, permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    /// <summary>Defers the default policy (used when an endpoint has [Authorize] with no policy name).</summary>
    /// <returns>The default provider's default policy.</returns>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackProvider.GetDefaultPolicyAsync();
    }

    /// <summary>Defers the fallback policy (the deny-by-default policy for unattributed endpoints).</summary>
    /// <returns>The default provider's fallback policy.</returns>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackProvider.GetFallbackPolicyAsync();
    }
}
