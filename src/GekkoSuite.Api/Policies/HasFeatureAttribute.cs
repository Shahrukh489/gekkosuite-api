using Microsoft.AspNetCore.Authorization;

using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Policies;

/// <summary>
/// Declares that an endpoint backs a paid capability: the org's effective features must include the given
/// feature code at the given scope. Encodes both into the policy name so PermissionPolicyProvider can turn
/// it back into a FeatureRequirement at request time. Stack it alongside HasPermission to gate on both.
/// </summary>
public class HasFeatureAttribute : AuthorizeAttribute
{
    /// <summary>Prefix marking a policy name this attribute owns, so the provider knows to parse it.</summary>
    public const string PolicyPrefix = "FEATURE_";

    /// <summary>
    /// Requires the org's effective features to include the given feature at the given scope.
    /// </summary>
    /// <param name="scope">The scope the feature applies at.</param>
    /// <param name="feature">The feature code the org must have.</param>
    public HasFeatureAttribute(MembershipScope scope, string feature)
    {
        Policy = $"{PolicyPrefix}{scope}:{feature}";
    }
}
