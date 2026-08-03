using System.Text.Json.Serialization;

using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Dtos;

public class MembershipDto
{
    /// <summary>The membership's id.</summary>
    public Guid MembershipId { get; set; }

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

    /// <summary>The role grants on this membership; one per role held.</summary>
    public List<MembershipAssignmentDto> Assignments { get; set; } = [];

    ///<summary>Map from MembershipEntity to MembershipDto </summary>
    public static MembershipDto FromEntity(MembershipEntity membershipEntity)
    {
        return new MembershipDto()
        {
            MembershipId = membershipEntity.MembershipId,
            Scope = membershipEntity.Scope,
            OrganizationId = membershipEntity.OrganizationId,
            StoreId = membershipEntity.StoreId,
            Name = membershipEntity.Name,
            Assignments = MembershipAssignmentDto.FromEntityList(membershipEntity.Assignments)
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
