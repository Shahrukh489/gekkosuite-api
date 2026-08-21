using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public interface IProductRepository
{
    /// <summary>
    /// Lists the live products at the given store (its own stock and price), by name.
    /// </summary>
    public Task<IEnumerable<ProductEntity>> GetStoreProductsAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Creates a product at the given store and returns the created row. Throws ConflictException if
    /// the SKU or barcode is already used at this store.
    /// </summary>
    public Task<ProductEntity> CreateProductAsync(CreateProductDto dto, Guid productId, DateTimeOffset now);

    /// <summary>
    /// Updates the given fields on a live product (null fields are left unchanged). Returns the updated
    /// row, or null if no live product matches. Throws ConflictException if the new SKU or barcode is
    /// already used at this store.
    /// </summary>
    public Task<ProductEntity?> UpdateProductAsync(UpdateProductDto dto, DateTimeOffset now);
}
