using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Services;

public interface IOrganizationService
{
    /// <summary>
    /// @TODO: return all subscriptions not just live so can show in UI
    /// Returns the organization record with its subscriptions and features
    /// </summary>
    Task<OrganizationDto?> GetOrganizationByIdAsync(Guid organizationId);

    /// <summary>
    /// Get the organizations users with their memberships
    /// </summary>
    Task<List<UserDto>> GetUsersAsync(Guid organizationId);

    /// <summary>
    /// Get all the available roles in the organization 
    /// </summary>
    Task<List<RoleDto>> GetRolesAsync(Guid organizationId);

    /// <summary>
    /// Returns a role with its details and permissions
    /// </summary>
    Task<RoleDto?> GetRoleByIdAsync(Guid organizationId, Guid roleId);

    /// <summary>
    /// Get all the stores in the organization
    /// </summary>
    Task<List<StoreDto>> GetStoresAsync(Guid organizationId);
}
