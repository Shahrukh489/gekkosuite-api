namespace GekkoSuite.Api.Entities;

public class ProductEntity
{
    /// <summary>The store_product's id.</summary>
    public Guid ProductId { get; set; }

    /// <summary>The store this product belongs to.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The organization that owns the store (the tenant).</summary>
    public Guid OrganizationId { get; set; }

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
}
