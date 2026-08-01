using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;


// @TODO: verify all of these queries
public interface IUserRepository
{
    /// <summary>
    /// Get a user's metadata
    /// </summary>
    public Task<UserEntity?> GetUserByIdAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Finds a live user by their login email
    /// </summary>
    public Task<UserEntity?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Get a user's organizationId
    /// </summary>
    public Task<Guid?> GetUserOrganizationIdAsync(Guid userId);

    /// <summary>
    /// Get a user's organization membership
    /// </summary>
    public Task<IEnumerable<MembershipEntity>> GetUserOrganizationMembershipsAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Get a user's memberships across all stores
    /// </summary>    
    public Task<IEnumerable<MembershipEntity>> GetUserAllStoresMembershipsAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Get a user's store membership
    /// </summary>
    public Task<IEnumerable<MembershipEntity>> GetUserStoreMembershipsAsync(Guid organizationId, Guid userId, Guid storeId);
}
