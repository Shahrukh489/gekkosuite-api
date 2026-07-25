using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IUserService
{
    /// <summary>
    /// Finds a live user by id within the given organization, or null if not found there.
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Lists the org's users, each with their memberships (role names + membership details); membership-less users included.
    /// </summary>
    Task<List<UserDto>> GetUsersWithMembershipsAsync(Guid organizationId);

    /// <summary>
    /// Lists the users with a live store membership at the given store, each with their store membership(s).
    /// </summary>
    Task<List<UserDto>> GetUsersByStoreIdWithMembershipsAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Returns the user's live ORGANIZATION memberships (one per role held), or null if they have none.
    /// </summary>
    Task<List<MembershipDto>?> GetUserOrganizationMembershipsByOrganizationIdAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's live STORE memberships (each with its store name and role), oldest-first.
    /// </summary>
    Task<List<MembershipDto>?> GetUserStoresMembershipsAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's live STORE memberships (one per role) at the given store, or null if none.
    /// </summary>
    Task<List<MembershipDto>?> GetUserStoreMembershipsByStoreIdAsync(Guid organizationId, Guid userId, Guid storeId);

    /// <summary>
    /// Builds the self-read view for the current user — their profile plus userType, defaultStoreId, and
    /// memberships (org entry, or store entries oldest-first). Null if the user isn't found.
    /// </summary>
    Task<UserDto?> GetUserByIdWithMembershipsAsync(Guid organizationId, Guid userId);
}
