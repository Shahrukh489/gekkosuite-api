using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IProductService
{
    /// <summary>
    /// Lists the products at the given store — the store's own catalog with its per-store stock and price.
    /// </summary>
    Task<List<ProductDto>> GetStoreProductsAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Creates a product at the given store. Throws BadRequestException for a blank name or a negative
    /// price, ConflictException if the SKU or barcode is already used at this store.
    /// </summary>
    Task<ProductDto> CreateProductAsync(Guid organizationId, Guid storeId, CreateProductRequest request);

    /// <summary>
    /// Updates the given fields on a live product (unset fields are left unchanged). Returns the
    /// updated product, or null if no live product matches. Throws BadRequestException for a blank name
    /// or a negative price, ConflictException if the new SKU or barcode is already used at this store.
    /// </summary>
    Task<ProductDto?> UpdateProductAsync(Guid organizationId, Guid storeId, Guid productId, UpdateProductRequest request);
}
