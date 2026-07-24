using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class MembershipDto
{
    /// <summary>"ORGANIZATION" or "STORE".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The organization id (set for an ORGANIZATION membership).</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>The store id (set for a STORE membership).</summary>
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

    ///<summary>Map from MembershipEntity to MembershipDto </summary>
    public MembershipDto FromEntity(MembershipEntity membershipEntity)
    {
        return new MembershipDto()
        {
            Type = membershipEntity.OrganizationId != null ? Constants.ORGANIZATION : Constants.STORE,
            OrganizationId = membershipEntity.OrganizationId,
            StoreId = membershipEntity.StoreId,
            Name = membershipEntity.Name,
            RoleId = membershipEntity.RoleId,
            RoleName = membershipEntity.RoleName,
            AssignedAt = membershipEntity.AssignedAt,
            ExpiresAt = membershipEntity.ExpiresAt
        };
    }
    ///<summary>Map from MembershipEntityList to MembershipDtoList </summary>
    public List<MembershipDto> FromEntityList(List<MembershipEntity> membershipEntities)
    {
        List<MembershipDto> membershipDtos = new List<MembershipDto>();

        for (int i = 0; i < membershipEntities.Count; i++)
        {
            membershipDtos.Add(new MembershipDto().FromEntity(membershipEntities[i]));
        }

        return membershipDtos;
    }
}
