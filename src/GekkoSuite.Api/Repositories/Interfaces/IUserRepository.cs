using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;


// @TODO: verify all of these queries
public interface IUserRepository
{
    /// <summary>
    /// Finds a live user by their login email
    /// </summary>
    public Task<UserEntity?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Resolves a live user's home organization id from their id alone (no tenant known yet). Used to
    /// establish the caller's org from the token's userId; null if not found.
    /// </summary>
    public Task<Guid?> GetUserOrganizationIdAsync(Guid userId);

    /// <summary>
    /// Finds a live user by id within the given organization, or null if not found there.
    /// </summary>
    public Task<UserEntity?> GetUserByIdAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Lists the users in the org, each with their memberships folded in; users with no membership included.
    /// </summary>
    public Task<IEnumerable<UserEntity>> GetOrganizationUsersWithMembershipsAsync(Guid organizationId);

    /// <summary>
    /// Lists the users with a live store membership at the given store, each with their store membership(s).
    /// </summary>
    public Task<IEnumerable<UserEntity>> GetStoreUsersWithMembershipsAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Returns the user's live ORGANIZATION membership rows (one per role held), empty if they have none.
    /// </summary>
    public Task<IEnumerable<MembershipEntity>> GetUserOrganizationMembershipsAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's live STORE memberships (each with its store name and role), oldest-first.
    /// </summary>
    public Task<IEnumerable<MembershipEntity>> GetUserStoresMembershipsAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's live STORE membership rows (one per role) at the given store, empty if none.
    /// </summary>
    public Task<IEnumerable<MembershipEntity>> GetUserStoreMembershipsAsync(Guid organizationId, Guid userId, Guid storeId);
}
