using System.Text.Json.Serialization;

using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Dtos;

public class RoleDto
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

    /// <summary>The permissions this role grants; populated on the detail view, omitted on the list.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PermissionDto>? Permissions { get; set; }

    /// <summary>Map from RoleEntity to RoleDto (summary only, no permissions).</summary>
    public RoleDto FromEntity(RoleEntity roleEntity)
    {
        return new RoleDto()
        {
            RoleId = roleEntity.RoleId,
            Name = roleEntity.Name,
            Description = roleEntity.Description,
            Scope = roleEntity.Scope,
            IsManaged = roleEntity.IsManaged,
        };
    }

    /// <summary>Map from a RoleEntity list to a RoleDto list (summaries only).</summary>
    public List<RoleDto> FromEntityList(List<RoleEntity> roleEntities)
    {
        List<RoleDto> roleDtos = new List<RoleDto>();

        for (int i = 0; i < roleEntities.Count; i++)
        {
            roleDtos.Add(new RoleDto().FromEntity(roleEntities[i]));
        }

        return roleDtos;
    }
}
