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

    /// <summary>The role grants on this membership (folded from json_agg); one per role held.</summary>
    public List<MembershipAssignmentEntity> Assignments { get; set; } = [];
}
