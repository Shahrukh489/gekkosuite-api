namespace GekkoSuite.Api.Dtos;

public class CreateProductRequest
{
    /// <summary>The product's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human description.</summary>
    public string? Description { get; set; }

    /// <summary>This store's own product code; optional, unique per store.</summary>
    public string? Sku { get; set; }

    /// <summary>Manufacturer barcode (UPC/EAN); optional, unique per store.</summary>
    public string? Barcode { get; set; }

    /// <summary>Free-text grouping, e.g. "Beverages"; optional.</summary>
    public string? Category { get; set; }

    /// <summary>Free-text brand/manufacturer; optional.</summary>
    public string? Brand { get; set; }

    /// <summary>This store's selling price.</summary>
    public decimal Price { get; set; }

    /// <summary>What this store paid per unit; optional.</summary>
    public decimal? Cost { get; set; }

    /// <summary>Starting units on hand at this store; defaults to 0.</summary>
    public int? Stock { get; set; }

    /// <summary>Whether a sale of this product is taxed; defaults to true.</summary>
    public bool? IsTaxable { get; set; }

    /// <summary>Tax percentage applied at checkout when taxable; defaults to 0.</summary>
    public decimal? TaxRate { get; set; }
}
