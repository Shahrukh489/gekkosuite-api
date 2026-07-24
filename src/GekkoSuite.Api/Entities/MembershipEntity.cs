namespace GekkoSuite.Api.Entities;

public class MembershipEntity
{
    /// <summary>The organization the membership is in (set for an ORGANIZATION membership).</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>The store the membership is in (set for a STORE membership).</summary>
    public Guid? StoreId { get; set; }

    /// <summary>The place's display name — the org name or the store name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The role id.</summary>
    public Guid? RoleId { get; set; }

    /// <summary>The role held on this membership.</summary>
    public string RoleName { get; set; } = string.Empty;
}