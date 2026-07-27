using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Entities;

public class RoleEntity
{
    /// <summary>The role's id.</summary>
    public Guid RoleId { get; set; }

    /// <summary>The role's display name (e.g. "Cashier", "Org Admin").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human description of what the role is for.</summary>
    public string? Description { get; set; }

    /// <summary>The level the role attaches at (ORGANIZATION or STORE).</summary>
    public MembershipScope Scope { get; set; }

    /// <summary>True for a system role we ship; false for an org's own custom role.</summary>
    public bool IsManaged { get; set; }

    /// <summary>The permissions this role grants (folded from json_agg); empty if none.</summary>
    public List<PermissionEntity> Permissions { get; set; } = [];
}
