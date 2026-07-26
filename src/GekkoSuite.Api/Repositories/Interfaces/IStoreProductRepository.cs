using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public interface IStoreProductRepository
{
    /// <summary>
    /// Lists the live products at the given store (its own stock and price), by name.
    /// </summary>
    public Task<IEnumerable<StoreProductEntity>> GetStoreProductsAsync(Guid organizationId, Guid storeId);
}
