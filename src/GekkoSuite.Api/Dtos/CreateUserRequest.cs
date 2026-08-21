using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Dtos;

public class CreateUserRequest
{
    /// <summary>The new user's first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>The new user's last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>The new user's login email; must be unique across the whole system.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Where the new membership is placed: the whole ORGANIZATION, or one STORE. Unset defaults to
    /// ORGANIZATION. Only an org admin calls this endpoint either way — placing a store membership here
    /// is still an org action, not something a store user does for themselves (see docs/api.md).
    /// </summary>
    public MembershipScope? Scope { get; set; }

    /// <summary>The store to place the membership at; required when Scope is STORE, omitted otherwise.</summary>
    public Guid? StoreId { get; set; }

    /// <summary>The role to grant — must match Scope (an ORGANIZATION role for an org membership, a STORE role for a store membership).</summary>
    public Guid? RoleId { get; set; }
}
