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
    /// Builds the full role view — the role plus the permissions it grants. Null if not visible to the org.
    /// </summary>
    Task<RoleDto?> GetRoleByIdAsync(Guid organizationId, Guid roleId);

    /// <summary>
    /// Builds the full role view at the given scope — the role plus its permissions. Null if not visible or scope mismatch.
    /// </summary>
    Task<RoleDto?> GetRoleByIdAndScopeAsync(Guid organizationId, Guid roleId, MembershipScope scope);
}
