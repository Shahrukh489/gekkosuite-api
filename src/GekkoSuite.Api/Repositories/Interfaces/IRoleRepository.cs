using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Repositories;

public interface IRoleRepository
{
    /// <summary>
    /// Gets all roles in an organization
    /// </summary>
    public Task<IEnumerable<RoleEntity>> GetRolesAsync(Guid organizationId, MembershipScope? scope);

    /// <summary>
    /// Get a role with its permissions 
    /// </summary>
    public Task<RoleEntity?> GetRoleByIdAsync(Guid organizationId, Guid roleId);
}
