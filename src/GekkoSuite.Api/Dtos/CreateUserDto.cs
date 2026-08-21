using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Dtos;

public class CreateUserDto
{
    /// <summary>The tenant the new user is created in (from the caller's token).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The admin creating this account (audit trail — user_account.created_by_user_id).</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>The new user's first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>The new user's last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>The new user's login email (lowercased before this point).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Where the new membership is placed: the whole ORGANIZATION, or one STORE.</summary>
    public MembershipScope Scope { get; set; }

    /// <summary>The store to place the membership at; set only when Scope is STORE.</summary>
    public Guid? StoreId { get; set; }

    /// <summary>The role to grant — must match Scope.</summary>
    public Guid RoleId { get; set; }
}
