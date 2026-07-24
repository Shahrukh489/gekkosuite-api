using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;


public interface IUserRepository
{
    /// <summary>
    /// Finds a live user by their login email
    /// </summary>
    public Task<UserEntity?> GetUserByEmailAsync(string email);

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
    public Task<IEnumerable<MembershipEntity>> GetUserStoreMembershipsAsync(Guid organizationId, Guid userId);
}
