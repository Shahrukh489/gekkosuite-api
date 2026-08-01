using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IUserService
{
    /// <summary>
    /// Get a user's metadata
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Get a user's organizationId
    /// </summary>
    Task<Guid?> GetUserOrganizationIdAsync(Guid userId);

    /// <summary>
    /// Get a user's organization membership
    /// </summary>
    Task<List<MembershipDto>?> GetUserOrganizationMembershipsAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Get a user's memberships across all stores
    /// </summary>
    Task<List<MembershipDto>?> GetUserStoresMembershipsAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Get a user's store membership
    /// </summary>
    Task<List<MembershipDto>?> GetUserStoreMembershipsAsync(Guid organizationId, Guid userId, Guid storeId);

    /// <summary>
    /// Get a user's metadata and memberships
    /// </summary>
    Task<UserDto?> GetUserWithMembershipsAsync(Guid organizationId, Guid userId);
}
