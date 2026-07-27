using System.Text.Json.Serialization;

using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Dtos;

public class MembershipDto
{
    /// <summary>The membership's id.</summary>
    public Guid MembershipId { get; set; }

    /// <summary>The membership-assignment's id (this specific role grant).</summary>
    public Guid AssignmentId { get; set; }

    /// <summary>The membership's scope (ORGANIZATION or STORE).</summary>
    public MembershipScope Scope { get; set; }

    /// <summary>The organization id (set for an ORGANIZATION membership); omitted for a STORE membership.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? OrganizationId { get; set; }

    /// <summary>The store id (set for a STORE membership); omitted for an ORGANIZATION membership.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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

    /// <summary>The permission codes this role grants, e.g. "product:read"; omitted when empty.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Permissions { get; set; }

    ///<summary>Map from MembershipEntity to MembershipDto </summary>
    public static MembershipDto FromEntity(MembershipEntity membershipEntity)
    {
        return new MembershipDto()
        {
            MembershipId = membershipEntity.MembershipId,
            AssignmentId = membershipEntity.AssignmentId,
            Scope = membershipEntity.Scope,
            OrganizationId = membershipEntity.OrganizationId,
            StoreId = membershipEntity.StoreId,
            Name = membershipEntity.Name,
            RoleId = membershipEntity.RoleId,
            RoleName = membershipEntity.RoleName,
            AssignedAt = membershipEntity.AssignedAt,
            ExpiresAt = membershipEntity.ExpiresAt,
            Permissions = membershipEntity.Permissions.Length > 0
                ? membershipEntity.Permissions.ToList()
                : null
        };
    }
    ///<summary>Map from MembershipEntityList to MembershipDtoList </summary>
    public static List<MembershipDto> FromEntityList(List<MembershipEntity> membershipEntities)
    {
        List<MembershipDto> membershipDtos = new List<MembershipDto>();

        for (int i = 0; i < membershipEntities.Count; i++)
        {
            membershipDtos.Add(FromEntity(membershipEntities[i]));
        }

        return membershipDtos;
    }
}
