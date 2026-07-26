using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class StoreProductDto
{
    /// <summary>The store_product's id.</summary>
    public Guid StoreProductId { get; set; }

    /// <summary>The store this product belongs to.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The product's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human description of the product.</summary>
    public string? Description { get; set; }

    /// <summary>The store's own product code; null if unset.</summary>
    public string? Sku { get; set; }

    /// <summary>This store's selling price (never shared across stores).</summary>
    public decimal Price { get; set; }

    /// <summary>Units on hand at this store (never shared across stores).</summary>
    public int Stock { get; set; }

    /// <summary>Listing toggle; false = hidden from selling but kept.</summary>
    public bool IsActive { get; set; }

    /// <summary>When the product was created at this store (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the product was last modified (stored UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Map from StoreProductEntity to StoreProductDto.</summary>
    /// <param name="storeProductEntity">The store_product row to map.</param>
    /// <returns>The client-safe product DTO.</returns>
    public static StoreProductDto FromEntity(StoreProductEntity storeProductEntity)
    {
        return new StoreProductDto()
        {
            StoreProductId = storeProductEntity.StoreProductId,
            StoreId = storeProductEntity.StoreId,
            Name = storeProductEntity.Name,
            Description = storeProductEntity.Description,
            Sku = storeProductEntity.Sku,
            Price = storeProductEntity.Price,
            Stock = storeProductEntity.Stock,
            IsActive = storeProductEntity.IsActive,
            CreatedAt = storeProductEntity.CreatedAt,
            UpdatedAt = storeProductEntity.UpdatedAt,
        };
    }
}
