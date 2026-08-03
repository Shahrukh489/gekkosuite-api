using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Policies;

/// <summary>
/// Builds authorization policies for HasPermission at request time: it parses the scope and permission
/// encoded in the policy name into the matching requirement, so no policy has to be pre-registered per
/// permission. Any other policy name falls through to the default provider.
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    /// <summary>
    /// Returns the policy for a name. HasPermission prefixed names are parsed into their requirement;
    /// everything else defers to the default provider.
    /// </summary>
    /// <param name="policyName">The policy name from the endpoint's attribute.</param>
    /// <returns>The built policy, or the default provider's result.</returns>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix))
        {
            return Task.FromResult<AuthorizationPolicy?>(BuildPermissionPolicy(policyName));
        }

        return _fallbackProvider.GetPolicyAsync(policyName);
    }

    /// <summary>
    /// Parses a HasPermission policy name ("scope:permission") into a policy carrying a PermissionRequirement.
    /// </summary>
    /// <param name="policyName">The prefixed policy name from HasPermissionAttribute.</param>
    /// <returns>The built authorization policy.</returns>
    private static AuthorizationPolicy BuildPermissionPolicy(string policyName)
    {
        string body = policyName.Substring(HasPermissionAttribute.PolicyPrefix.Length);
        int separator = body.IndexOf(':');

        MembershipScope scope = Enum.Parse<MembershipScope>(body.Substring(0, separator));
        string permission = body.Substring(separator + 1);

        return new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(scope, permission))
            .Build();
    }

    /// <summary>
    /// Parses a HasFeature policy name ("scope:feature") into a policy carrying a FeatureRequirement.
    /// </summary>
    /// <param name="policyName">The prefixed policy name from HasFeatureAttribute.</param>
    /// <returns>The built authorization policy.</returns>
    private static AuthorizationPolicy BuildFeaturePolicy(string policyName)
    {
        string body = policyName.Substring(HasFeatureAttribute.PolicyPrefix.Length);
        int separator = body.IndexOf(':');

        MembershipScope scope = Enum.Parse<MembershipScope>(body.Substring(0, separator));
        string feature = body.Substring(separator + 1);

        return new AuthorizationPolicyBuilder()
            .AddRequirements(new FeatureRequirement(feature, scope))
            .Build();
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
