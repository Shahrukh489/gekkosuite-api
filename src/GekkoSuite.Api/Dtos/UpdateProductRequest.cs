namespace GekkoSuite.Api.Dtos;

public class UpdateProductRequest
{
    /// <summary>New display name; unset leaves the current value.</summary>
    public string? Name { get; set; }

    /// <summary>New description; unset leaves the current value.</summary>
    public string? Description { get; set; }

    /// <summary>New SKU; unset leaves the current value.</summary>
    public string? Sku { get; set; }

    /// <summary>New barcode; unset leaves the current value.</summary>
    public string? Barcode { get; set; }

    /// <summary>New category; unset leaves the current value.</summary>
    public string? Category { get; set; }

    /// <summary>New brand; unset leaves the current value.</summary>
    public string? Brand { get; set; }

    /// <summary>New selling price; unset leaves the current value.</summary>
    public decimal? Price { get; set; }

    /// <summary>New per-unit cost; unset leaves the current value.</summary>
    public decimal? Cost { get; set; }

    /// <summary>New stock count (a manual correction — see product.md); unset leaves the current value.</summary>
    public int? Stock { get; set; }

    /// <summary>New taxable flag; unset leaves the current value.</summary>
    public bool? IsTaxable { get; set; }

    /// <summary>New tax rate; unset leaves the current value.</summary>
    public decimal? TaxRate { get; set; }

    /// <summary>New sellable (listing) toggle; unset leaves the current value.</summary>
    public bool? IsActive { get; set; }
}
