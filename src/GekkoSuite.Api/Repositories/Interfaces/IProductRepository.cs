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
    /// Creates a product at the given store and returns the created row — standalone, added as a new
    /// variant into an existing group (dto.GroupId set), or as a new group's first variant (dto.NewGroup
    /// set, created atomically with the group). Throws ConflictException if the SKU or barcode is
    /// already used at this store, or BadRequestException if dto.GroupId doesn't resolve to a live group
    /// at this store.
    /// </summary>
    public Task<ProductEntity> CreateProductAsync(CreateProductDto dto, Guid productId, DateTimeOffset now);

    /// <summary>
    /// Updates the given fields on a live product (null fields are left unchanged). Returns the updated
    /// row, or null if no live product matches. Throws ConflictException if the new SKU or barcode is
    /// already used at this store. Name/description/category/brand are silently ignored if the row is
    /// grouped (defense-in-depth backstop — the service is the primary check).
    /// </summary>
    public Task<ProductEntity?> UpdateProductAsync(UpdateProductDto dto, DateTimeOffset now);

    /// <summary>
    /// Updates the given fields on a live product group (null fields are left unchanged). Returns the
    /// updated group, or null if no live group matches.
    /// </summary>
    public Task<StoreProductGroupEntity?> UpdateProductGroupAsync(UpdateProductGroupDto dto, DateTimeOffset now);
}
