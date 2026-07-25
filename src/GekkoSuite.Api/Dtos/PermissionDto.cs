using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class PermissionDto
{
    /// <summary>The permission's id.</summary>
    public Guid PermissionId { get; set; }

    /// <summary>The thing acted on, e.g. "product", "sale", "role".</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>What may be done to it, e.g. "read", "create", "refund".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>True for a company-level permission that may only sit in an ORGANIZATION role.</summary>
    public bool IsElevated { get; set; }

    /// <summary>Map from PermissionEntity to PermissionDto.</summary>
    public PermissionDto FromEntity(PermissionEntity permissionEntity)
    {
        return new PermissionDto()
        {
            PermissionId = permissionEntity.PermissionId,
            Resource = permissionEntity.Resource,
            Action = permissionEntity.Action,
            IsElevated = permissionEntity.IsElevated,
        };
    }

    /// <summary>Map from a PermissionEntity list to a PermissionDto list.</summary>
    public List<PermissionDto> FromEntityList(List<PermissionEntity> permissionEntities)
    {
        List<PermissionDto> permissionDtos = new List<PermissionDto>();

        for (int i = 0; i < permissionEntities.Count; i++)
        {
            permissionDtos.Add(new PermissionDto().FromEntity(permissionEntities[i]));
        }

        return permissionDtos;
    }
}
