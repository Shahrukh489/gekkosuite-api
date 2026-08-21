using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IUserService
{
    /// <summary>
    /// Finds a live user by id within the given organization, or null if not found there.
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Resolves a user's home organization from their id alone (no tenant known yet). Returns null if the
    /// user does not exist. Used to establish the caller's org from the token's userId.
    /// </summary>
    Task<Guid?> GetUserOrganizationIdAsync(Guid userId);

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

    /// <summary>
    /// Creates an organization user: a login plus a live ORGANIZATION membership under the given role.
    /// Generates a one-time temporary password (there is no invite-email flow yet — docs/auth.md's
    /// Security Review Notes) and returns it alongside the created user's safe view. Throws
    /// BadRequestException for invalid input or an unassignable role, ConflictException if the email is
    /// already taken.
    /// </summary>
    Task<(UserDto User, string TemporaryPassword)> CreateUserAsync(CreateUserDto dto);

    /// <summary>
    /// Updates the given fields on a live organization user (null fields are left unchanged). Returns
    /// the updated user's view, or null if no live user matches both ids. Throws BadRequestException for
    /// a blank field, ConflictException if the new email is already taken.
    /// </summary>
    Task<UserDto?> UpdateUserAsync(UpdateUserDto dto);
}
