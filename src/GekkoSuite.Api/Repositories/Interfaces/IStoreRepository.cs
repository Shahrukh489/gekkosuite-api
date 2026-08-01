using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public interface IStoreRepository
{

    /// <summary>
    /// Finds a live store by id within the given organization, or null if it isn't in that org.
    /// </summary>
    public Task<StoreEntity?> GetStoreByIdAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Lists the users with a live store membership at the given store (metadata only, no memberships).
    /// </summary>
    public Task<IEnumerable<UserEntity>> GetStoreUsersAsync(Guid organizationId, Guid storeId);
}
