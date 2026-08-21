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
    /// Returns one user in the org — their record plus their memberships — or null if not found there.
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Creates an organization user (login plus a live ORGANIZATION membership under the given role) and
    /// returns it with its one-time temporary password. Throws BadRequestException for invalid input or
    /// an unassignable role, ConflictException if the email is already taken.
    /// </summary>
    Task<CreateUserResponse> CreateUserAsync(Guid organizationId, Guid createdByUserId, CreateUserRequest request);

    /// <summary>
    /// Updates the given fields on a live organization user (unset fields are left unchanged). Returns
    /// the updated user, or null if no live user matches both ids. Throws BadRequestException for a
    /// blank field, ConflictException if the new email is already taken.
    /// </summary>
    Task<UserDto?> UpdateUserAsync(Guid organizationId, Guid userId, UpdateUserRequest request);

    /// <summary>
    /// Lists the roles assignable in the org (managed + the org's own), optionally filtered to one scope.
    /// </summary>
    Task<List<RoleDto>> GetRolesAsync(Guid organizationId, MembershipScope? scope);

    /// <summary>
    /// Returns one role and the permissions it grants, or null if not visible to the org.
    /// </summary>
    Task<RoleDto?> GetRoleByIdAsync(Guid organizationId, Guid roleId);

    /// <summary>
    /// Lists the stores in the org (the org's roster).
    /// </summary>
    Task<List<StoreDto>> GetStoresAsync(Guid organizationId);

    /// <summary>
    /// Summarizes the org's day across every store for the org dashboard.
    /// </summary>
    Task<OrganizationDashboardSummaryDto> GetDashboardSummaryAsync(Guid organizationId);
}
