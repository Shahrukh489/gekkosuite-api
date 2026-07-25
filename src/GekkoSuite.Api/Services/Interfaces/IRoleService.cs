using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Services;

public interface IRoleService
{
    /// <summary>
    /// Lists the roles assignable in the org (managed + the org's own), optionally filtered to one scope.
    /// </summary>
    Task<List<RoleDto>> GetRolesAsync(Guid organizationId, MembershipScope? scope);

    /// <summary>
    /// Builds the full role view — the role plus the permissions it grants, optionally filtered to a scope.
    /// Null if not visible to the org or the scope doesn't match.
    /// </summary>
    Task<RoleDto?> GetRoleByIdAsync(Guid organizationId, Guid roleId, MembershipScope? scope = null);
}
