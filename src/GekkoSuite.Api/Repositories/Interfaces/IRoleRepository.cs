using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Repositories;

public interface IRoleRepository
{
    /// <summary>
    /// Lists the roles assignable in the org — managed roles plus the org's own custom roles,
    /// optionally filtered to one scope (ORGANIZATION or STORE).
    /// </summary>
    public Task<IEnumerable<RoleEntity>> GetRolesAsync(Guid organizationId, MembershipScope? scope);

    /// <summary>
    /// Finds one role by id, visible only if it is managed or owned by the given org; null otherwise.
    /// </summary>
    public Task<RoleEntity?> GetRoleByIdAsync(Guid organizationId, Guid roleId);
}
