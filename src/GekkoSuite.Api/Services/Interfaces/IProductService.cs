using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IProductService
{
    /// <summary>
    /// Lists the products at the given store — the store's own catalog with its per-store stock and price.
    /// </summary>
    Task<List<ProductDto>> GetStoreProductsAsync(Guid organizationId, Guid storeId);
}
