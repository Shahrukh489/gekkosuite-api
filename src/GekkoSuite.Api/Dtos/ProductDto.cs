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

    /// <summary>Manufacturer barcode (UPC/EAN); null if unset. Distinct from Sku.</summary>
    public string? Barcode { get; set; }

    /// <summary>Free-text grouping for browsing/filtering; null if unset.</summary>
    public string? Category { get; set; }

    /// <summary>This store's selling price (never shared across stores).</summary>
    public decimal Price { get; set; }

    /// <summary>What this store paid per unit; null if unset (never shared across stores).</summary>
    public decimal? Cost { get; set; }

    /// <summary>Units on hand at this store (never shared across stores).</summary>
    public int Stock { get; set; }

    /// <summary>Whether a sale of this product is taxed; false = always tax-exempt regardless of TaxRate.</summary>
    public bool IsTaxable { get; set; }

    /// <summary>Tax percentage applied at checkout when IsTaxable (e.g. 8.25 = 8.25%).</summary>
    public decimal TaxRate { get; set; }

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
            Barcode = productEntity.Barcode,
            Category = productEntity.Category,
            Price = productEntity.Price,
            Cost = productEntity.Cost,
            Stock = productEntity.Stock,
            IsTaxable = productEntity.IsTaxable,
            TaxRate = productEntity.TaxRate,
            IsActive = productEntity.IsActive,
            CreatedAt = productEntity.CreatedAt,
            UpdatedAt = productEntity.UpdatedAt,
        };
    }
}
