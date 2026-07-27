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
    /// Finds one role by id, optionally filtered to a scope (ORGANIZATION or STORE); visible only if it is
    /// managed or owned by the given org; null if not found or the scope doesn't match.
    /// </summary>
    public Task<RoleEntity?> GetRoleByIdAsync(Guid organizationId, Guid roleId, MembershipScope? scope = null);
}
