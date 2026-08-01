using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Services;

public interface IOrganizationService
{
    /// <summary>
    /// Get the organization details with its subscriptions and features
    /// </summary>
    Task<OrganizationDto?> GetOrganizationByIdAsync(Guid organizationId);

    /// <summary>
    /// Get the organizations users with their memberships
    /// </summary>
    Task<List<UserDto>> GetOrganizationUsersAsync(Guid organizationId);

    /// <summary>
    /// Get all the available roles in the organization 
    /// </summary>
    Task<List<RoleDto>> GetOrganizationRolesAsync(Guid organizationId);

    /// <summary>
    /// Returns a role with its details and permissions
    /// </summary>
    Task<RoleDto?> GetOrganizationRoleByIdAsync(Guid organizationId, Guid roleId);

    /// <summary>
    /// Get all the stores in the organization
    /// </summary>
    Task<List<StoreDto>> GetOrganizationStoresAsync(Guid organizationId);
}
