using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Services;

public interface IRoleService
{
    /// <summary>
    /// Gets all roles in an organization
    /// </summary>
    Task<List<RoleDto>> GetRolesAsync(Guid organizationId);

    /// <summary>
    /// Get a role with its permissions 
    /// </summary>
    Task<RoleDto?> GetRoleByIdAsync(Guid organizationId, Guid roleId);
}
