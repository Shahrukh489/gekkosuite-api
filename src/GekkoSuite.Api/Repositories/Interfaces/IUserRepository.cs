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
