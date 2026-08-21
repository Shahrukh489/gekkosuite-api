using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IProductService
{
    /// <summary>
    /// Lists the products at the given store — the store's own catalog with its per-store stock and price.
    /// </summary>
    Task<List<ProductDto>> GetStoreProductsAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Creates a product at the given store — standalone, added as a new variant into an existing group
    /// (request.GroupId), or as a new group's first variant (request.NewGroup). Throws
    /// BadRequestException for a blank name/negative price, for setting both GroupId and NewGroup, for
    /// setting name/description/category/brand while grouped, or if GroupId doesn't resolve to a live
    /// group at this store. Throws ConflictException if the SKU or barcode is already used at this store.
    /// </summary>
    Task<ProductDto> CreateProductAsync(Guid organizationId, Guid storeId, CreateProductRequest request);

    /// <summary>
    /// Updates the given fields on a live product (unset fields are left unchanged). Returns the
    /// updated product, or null if no live product matches. Throws BadRequestException for a blank name
    /// or a negative price, ConflictException if the new SKU or barcode is already used at this store.
    /// </summary>
    Task<ProductDto?> UpdateProductAsync(Guid organizationId, Guid storeId, Guid productId, UpdateProductRequest request);

    /// <summary>
    /// Updates the given fields on a live product group (unset fields are left unchanged). Returns the
    /// updated group summary, or null if no live group matches. Throws BadRequestException for a blank name.
    /// </summary>
    Task<ProductGroupSummaryDto?> UpdateProductGroupAsync(Guid organizationId, Guid storeId, Guid groupId, UpdateProductGroupRequest request);
}
