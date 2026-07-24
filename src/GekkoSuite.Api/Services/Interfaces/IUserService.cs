using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Services;

public interface IUserService
{
    /// <summary>
    /// Finds a live user by id within the given organization, or null if not found there.
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's live ORGANIZATION memberships (one per role held), or null if they have none.
    /// </summary>
    Task<List<MembershipDto>?> GetUserOrganizationMembershipsAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's live STORE memberships (each with its store name and role), oldest-first.
    /// </summary>
    Task<List<MembershipDto>?> GetUserStoreMembershipsAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's live STORE memberships (one per role) at the given store, or null if none.
    /// </summary>
    Task<List<MembershipDto>?> GetUserStoreMembershipsByStoreIdAsync(Guid organizationId, Guid userId, Guid storeId);

    /// <summary>
    /// Returns the flat, deduped set of permission codes the user holds across their organization roles.
    /// </summary>
    Task<List<string>> GetUserOrganizationPermissionsAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the flat, deduped set of permission codes the user holds at the given store.
    /// </summary>
    Task<List<string>> GetUserStorePermissionsByStoreIdAsync(Guid organizationId, Guid userId, Guid storeId);

    /// <summary>
    /// Builds the self-read view for the current user — their profile plus userType, defaultStoreId, and
    /// memberships (org entry, or store entries oldest-first). Null if the user isn't found.
    /// </summary>
    Task<UserDto?> GetUserAsync(Guid organizationId, Guid userId);
}
