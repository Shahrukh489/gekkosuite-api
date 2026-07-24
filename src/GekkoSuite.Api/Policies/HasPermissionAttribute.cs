using Microsoft.AspNetCore.Authorization;

using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Policies;

/// <summary>
/// Declares an endpoint's authorization requirement: the membership scope the caller must hold, and
/// optionally a permission their role must grant. Encodes both into the policy name so
/// PermissionPolicyProvider can turn it back into a PermissionRequirement at request time.
/// </summary>
public class HasPermissionAttribute : AuthorizeAttribute
{
    /// <summary>Prefix marking a policy name this attribute owns, so the provider knows to parse it.</summary>
    public const string PolicyPrefix = "PERMISSION_";

    /// <summary>
    /// Requires the caller to hold a membership of the given scope, and (if provided) a role granting the permission.
    /// </summary>
    public HasPermissionAttribute(MembershipScope scope, string? permission = null)
    {
        Policy = $"{PolicyPrefix}{scope}:{permission}";
    }
}
