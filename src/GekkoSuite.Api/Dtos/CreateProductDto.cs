namespace GekkoSuite.Api.Dtos;

public class CreateProductDto
{
    /// <summary>The tenant the store belongs to (from the caller's token).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The store this product is being added at.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The product's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human description.</summary>
    public string? Description { get; set; }

    /// <summary>This store's own product code; optional.</summary>
    public string? Sku { get; set; }

    /// <summary>Manufacturer barcode; optional.</summary>
    public string? Barcode { get; set; }

    /// <summary>Free-text grouping; optional.</summary>
    public string? Category { get; set; }

    /// <summary>Free-text brand/manufacturer; optional.</summary>
    public string? Brand { get; set; }

    /// <summary>This store's selling price.</summary>
    public decimal Price { get; set; }

    /// <summary>What this store paid per unit; optional.</summary>
    public decimal? Cost { get; set; }

    /// <summary>Starting units on hand.</summary>
    public int Stock { get; set; }

    /// <summary>Whether a sale of this product is taxed.</summary>
    public bool IsTaxable { get; set; }

    /// <summary>Tax percentage applied at checkout when taxable.</summary>
    public decimal TaxRate { get; set; }
}
