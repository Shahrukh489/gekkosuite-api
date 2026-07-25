using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IStoreService
{
    /// <summary>
    /// Lists the stores in the given organization (the org's roster), default store first.
    /// </summary>
    Task<List<StoreDto>> GetStoresAsync(Guid organizationId);

    /// <summary>
    /// Finds a store by id within the given organization, or null if it isn't in that org.
    /// </summary>
    Task<StoreDto?> GetStoreByIdAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Returns the org's deduped STORE-scoped feature codes across its live subscriptions.
    /// </summary>
    Task<List<string>> GetStoreFeaturesAsync(Guid organizationId);

    /// <summary>
    /// Builds the full store view — the record plus its store-scoped features. Null if not found in the org.
    /// </summary>
    Task<StoreDto?> GetStoreAsync(Guid organizationId, Guid storeId);
}
