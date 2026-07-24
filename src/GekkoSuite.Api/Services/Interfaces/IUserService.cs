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
    /// Returns the user's single live ORGANIZATION membership (with its role), or null if they have none.
    /// </summary>
    Task<MembershipDto?> GetOrganizationMembershipAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Returns the user's live STORE memberships (each with its store name and role), oldest-first.
    /// </summary>
    Task<List<MembershipDto>?> GetStoreMembershipsAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Builds the self-read view for the current user — their profile plus userType, defaultStoreId, and
    /// memberships (org entry, or store entries oldest-first). Null if the user isn't found.
    /// </summary>
    Task<UserDto?> GetUserAsync(Guid organizationId, Guid userId);
}
