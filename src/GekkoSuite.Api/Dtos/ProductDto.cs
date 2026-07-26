using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class ProductDto
{
    /// <summary>The store_product's id.</summary>
    public Guid ProductId { get; set; }

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

    /// <summary>Map from ProductEntity to ProductDto.</summary>
    /// <param name="productEntity">The store_product row to map.</param>
    /// <returns>The client-safe product DTO.</returns>
    public static ProductDto FromEntity(ProductEntity productEntity)
    {
        return new ProductDto()
        {
            ProductId = productEntity.ProductId,
            StoreId = productEntity.StoreId,
            Name = productEntity.Name,
            Description = productEntity.Description,
            Sku = productEntity.Sku,
            Price = productEntity.Price,
            Stock = productEntity.Stock,
            IsActive = productEntity.IsActive,
            CreatedAt = productEntity.CreatedAt,
            UpdatedAt = productEntity.UpdatedAt,
        };
    }
}
