using Microsoft.AspNetCore.Authorization;

using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Policies;

/// <summary>
/// Declares what an endpoint requires to authorize: the membership scope the caller must hold, and the
/// permission one of their roles must grant. Every endpoint gates on a permission.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>The membership scope the endpoint acts at (ORGANIZATION or STORE).</summary>
    public MembershipScope Scope { get; }

    /// <summary>The permission code the caller's role must grant (e.g. "store:read").</summary>
    public string Permission { get; }

    /// <summary>
    /// Creates the requirement with the scope it acts at and the required permission.
    /// </summary>
    /// <param name="scope">The membership scope the endpoint acts at.</param>
    /// <param name="permission">The permission the role must grant.</param>
    public PermissionRequirement(MembershipScope scope, string permission)
    {
        Scope = scope;
        Permission = permission;
    }
}
