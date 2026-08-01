using System.Text.Json.Serialization;

using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class MembershipAssignmentDto
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

    /// <summary>The permission codes this role grants, e.g. "product:read"; omitted when empty.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Permissions { get; set; }

    /// <summary>Map from a MembershipAssignmentEntity to a MembershipAssignmentDto.</summary>
    public static MembershipAssignmentDto FromEntity(MembershipAssignmentEntity assignmentEntity)
    {
        return new MembershipAssignmentDto()
        {
            AssignmentId = assignmentEntity.AssignmentId,
            RoleId = assignmentEntity.RoleId,
            RoleName = assignmentEntity.RoleName,
            AssignedAt = assignmentEntity.AssignedAt,
            ExpiresAt = assignmentEntity.ExpiresAt,
            // the repo returns raw permission rows; collapse each to its resource:action code here
            Permissions = assignmentEntity.Permissions.Count > 0
                ? assignmentEntity.Permissions.Select(permission => $"{permission.Resource}:{permission.Action}").ToList()
                : null
        };
    }

    /// <summary>Map from a MembershipAssignmentEntity list to a MembershipAssignmentDto list.</summary>
    public static List<MembershipAssignmentDto> FromEntityList(List<MembershipAssignmentEntity> assignmentEntities)
    {
        List<MembershipAssignmentDto> assignmentDtos = new List<MembershipAssignmentDto>();

        for (int i = 0; i < assignmentEntities.Count; i++)
        {
            assignmentDtos.Add(FromEntity(assignmentEntities[i]));
        }

        return assignmentDtos;
    }
}
