using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Services;

public interface IOrganizationService
{
    /// <summary>
    /// Returns the organization record with its live subscriptions and org-scoped features, or null if not found.
    /// </summary>
    Task<OrganizationDto?> GetOrganizationByIdAsync(Guid organizationId);

    /// <summary>
    /// Lists the org's users, each with their memberships (role names + membership details).
    /// </summary>
    Task<List<UserDto>> GetUsersAsync(Guid organizationId);

    /// <summary>
    /// Lists the roles assignable in the org (managed + the org's own), optionally filtered to one scope.
    /// </summary>
    Task<List<RoleDto>> GetRolesAsync(Guid organizationId);

    /// <summary>
    /// Returns one role and the permissions it grants, or null if not visible to the org.
    /// </summary>
    Task<RoleDto?> GetRoleByIdAsync(Guid organizationId, Guid roleId);

    /// <summary>
    /// Lists the stores in the org (the org's roster).
    /// </summary>
    Task<List<StoreDto>> GetStoresAsync(Guid organizationId);
}
