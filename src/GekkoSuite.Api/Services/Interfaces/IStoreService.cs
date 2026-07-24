using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IStoreService
{
    /// <summary>
    /// Finds a store by id within the given organization, or null if it isn't in that org.
    /// </summary>
    Task<StoreDto?> GetStoreByIdAsync(Guid organizationId, Guid storeId);
}
