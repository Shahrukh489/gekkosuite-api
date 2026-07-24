namespace GekkoSuite.Api.Repositories;

public class UserEntity
{
    /// <summary>The user's id.</summary>
    public Guid UserId { get; set; }

    /// <summary>The user's home org (set once, immutable) — the tenant boundary.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Login identifier; globally unique across the whole system (one email = one account).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Argon2id password hash. Never plaintext — see docs/auth.md's Security Review, R2.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>First name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Contact phone; optional.</summary>
    public string? Phone { get; set; }

    /// <summary>Account kill switch; false = every membership is suspended (the way to revoke, not delete).</summary>
    public bool IsActive { get; set; }

    /// <summary>True for the org owner — full access that can't be stripped, only transferred.</summary>
    public bool IsOrgOwner { get; set; }

    /// <summary>The admin who created this account; null if system-seeded.</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>When the account was created (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Soft-delete flag; true = removed but kept for history.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the account was soft-deleted (stored UTC); null while active.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}


public class MembershipEntity
{
    /// <summary>The organization the membership is in (set for an ORGANIZATION membership).</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>The store the membership is in (set for a STORE membership).</summary>
    public Guid? StoreId { get; set; }

    /// <summary>The place's display name — the org name or the store name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The role held on this membership.</summary>
    public string RoleName { get; set; } = string.Empty;
}

public interface IUserRepository
{
    /// <summary>
    /// Finds a user by their login email.
    /// </summary>
    public Task<UserEntity?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Finds a live user by id within the given organization, or null if not found there.
    /// </summary>
    public Task<UserEntity?> GetUserByIdAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's single live ORGANIZATION membership (with its role), or null if they have none.
    /// </summary>
    public Task<MembershipEntity?> GetOrgMembershipAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's live STORE memberships (each with its store name and role), oldest-first.
    /// </summary>
    public Task<IEnumerable<MembershipEntity>> GetStoreMembershipsAsync(Guid organizationId, Guid userId);
}
