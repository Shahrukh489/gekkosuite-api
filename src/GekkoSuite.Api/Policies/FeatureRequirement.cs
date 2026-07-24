using Microsoft.AspNetCore.Authorization;

using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Policies;

/// <summary>
/// Declares that an endpoint backs a paid capability: the org's effective features must include the given
/// feature code at the given scope. Handled by FeatureHandler.
/// </summary>
public class FeatureRequirement : IAuthorizationRequirement
{
    /// <summary>The feature code the org's offerings must include (e.g. "returns").</summary>
    public string Feature { get; }

    /// <summary>The scope the feature applies at; the same code can exist at both STORE and ORGANIZATION.</summary>
    public MembershipScope Scope { get; }

    /// <summary>
    /// Creates the requirement for a feature code at a scope.
    /// </summary>
    public FeatureRequirement(string feature, MembershipScope scope)
    {
        Feature = feature;
        Scope = scope;
    }
}
