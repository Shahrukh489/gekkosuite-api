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

    /// <summary>Manufacturer barcode (UPC/EAN); null if unset. Distinct from Sku.</summary>
    public string? Barcode { get; set; }

    /// <summary>Free-text grouping for browsing/filtering; null if unset.</summary>
    public string? Category { get; set; }

    /// <summary>Free-text brand/manufacturer; null if unset.</summary>
    public string? Brand { get; set; }

    /// <summary>The variant family this row belongs to; null if this is a standalone product.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>This variant's own value along the group's first dimension, e.g. "Vanilla"; null if unset.</summary>
    public string? VariantOptionOneValue { get; set; }

    /// <summary>This variant's own value along the group's second dimension; null if unset.</summary>
    public string? VariantOptionTwoValue { get; set; }

    /// <summary>This variant's own value along the group's third dimension; null if unset.</summary>
    public string? VariantOptionThreeValue { get; set; }

    /// <summary>The group's name, when GroupId is set — used to build the nested group summary.</summary>
    public string? GroupName { get; set; }

    /// <summary>The group's description, when GroupId is set.</summary>
    public string? GroupDescription { get; set; }

    /// <summary>The group's category, when GroupId is set.</summary>
    public string? GroupCategory { get; set; }

    /// <summary>The group's brand, when GroupId is set.</summary>
    public string? GroupBrand { get; set; }

    /// <summary>The group's first variant dimension name, when GroupId is set.</summary>
    public string? GroupVariantOptionOneName { get; set; }

    /// <summary>The group's second variant dimension name, when GroupId is set.</summary>
    public string? GroupVariantOptionTwoName { get; set; }

    /// <summary>The group's third variant dimension name, when GroupId is set.</summary>
    public string? GroupVariantOptionThreeName { get; set; }

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
}
