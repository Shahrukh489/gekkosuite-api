namespace GekkoSuite.Api.Entities;

public class MembershipAssignmentEntity
{
    /// <summary>The membership-assignment's id (this specific role grant).</summary>
    public Guid AssignmentId { get; set; }

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
