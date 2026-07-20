namespace GekkoSuite.Api.Models;

/// <summary>
/// Maps a row of the "user" table (docs/database.md) — a login, nothing more. Where a user can act
/// comes from their memberships (see docs/auth.md), which are not part of this record.
/// </summary>
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

    /// <summary>Display name shown in the UI.</summary>
    public string Name { get; set; } = string.Empty;

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
