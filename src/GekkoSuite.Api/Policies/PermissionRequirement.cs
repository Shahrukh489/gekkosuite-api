using Microsoft.AspNetCore.Authorization;

using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Policies;

/// <summary>
/// Declares what an endpoint requires to authorize: the membership scope the caller must hold, and
/// optionally a permission their role must grant. A null permission means holding the membership is enough.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>The membership scope the endpoint acts at (ORGANIZATION or STORE).</summary>
    public MembershipScope Scope { get; }

    /// <summary>The permission code the caller's role must grant (e.g. "store:delete"); null if none is required.</summary>
    public string? Permission { get; }

    /// <summary>
    /// Creates the requirement with the scope it acts at and an optional required permission.
    /// </summary>
    /// <param name="scope">The membership scope the endpoint acts at.</param>
    /// <param name="permission">The permission the role must grant, or null if membership alone suffices.</param>
    public PermissionRequirement(MembershipScope scope, string? permission = null)
    {
        Scope = scope;
        Permission = permission;
    }
}
