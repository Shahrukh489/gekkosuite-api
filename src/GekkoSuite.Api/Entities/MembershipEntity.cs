using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Entities;

public class MembershipEntity
{
    /// <summary>The membership's id.</summary>
    public Guid MembershipId { get; set; }

    /// <summary>The membership's scope (ORGANIZATION or STORE).</summary>
    public MembershipScope Scope { get; set; }

    /// <summary>The organization the membership is in (set for an ORGANIZATION membership).</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>The store the membership is in (set for a STORE membership).</summary>
    public Guid? StoreId { get; set; }

    /// <summary>The place's display name — the org name or the store name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The granted role's id.</summary>
    public Guid RoleId { get; set; }

    /// <summary>The granted role's display name (e.g. "Cashier", "Org Admin").</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>When the role was granted (stored UTC).</summary>
    public DateTimeOffset AssignedAt { get; set; }

    /// <summary>Optional expiry; null = never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>The permission codes (resource:action) this role grants, e.g. "product:read".</summary>
    public string[] Permissions { get; set; } = [];
}