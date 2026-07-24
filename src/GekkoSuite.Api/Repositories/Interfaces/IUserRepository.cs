using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;


public interface IUserRepository
{
    /// <summary>
    /// Finds a live user by id within the given organization, or null if not found there.
    /// </summary>
    public Task<UserEntity?> GetUserByIdAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's single live ORGANIZATION membership (with its role), or null if they have none.
    /// </summary>
    public Task<MembershipEntity?> GetOrganizationMembershipAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's live STORE memberships (each with its store name and role), oldest-first.
    /// </summary>
    public Task<IEnumerable<MembershipEntity>> GetStoreMembershipsAsync(Guid organizationId, Guid userId);
}
