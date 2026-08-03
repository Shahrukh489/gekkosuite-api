using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IStoreService
{
    /// <summary>
    /// Get all the users in a store with there memberships
    /// </summary>
    Task<List<UserDto>> GetStoreUsersAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Get a store's details
    /// </summary>
    Task<StoreDto?> GetStoreByIdAsync(Guid organizationId, Guid storeId);
}
