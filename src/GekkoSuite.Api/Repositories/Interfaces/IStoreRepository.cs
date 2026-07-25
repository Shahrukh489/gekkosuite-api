using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public interface IStoreRepository
{
    /// <summary>
    /// Lists the live stores in the given organization (the org's roster), default store first.
    /// </summary>
    public Task<IEnumerable<StoreEntity>> GetStoresAsync(Guid organizationId);

    /// <summary>
    /// Finds a live store by id within the given organization, or null if it isn't in that org.
    /// </summary>
    public Task<StoreEntity?> GetStoreByIdAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Returns the deduped STORE-scoped feature codes the org's live subscriptions' offerings enable.
    /// </summary>
    public Task<IEnumerable<string>> GetStoreFeaturesAsync(Guid organizationId);
}
