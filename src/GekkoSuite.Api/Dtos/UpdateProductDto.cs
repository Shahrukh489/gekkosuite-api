namespace GekkoSuite.Api.Dtos;

public class UpdateProductDto
{
    /// <summary>The tenant the store belongs to (from the caller's token).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The store the product belongs to.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The product being updated.</summary>
    public Guid ProductId { get; set; }

    /// <summary>New display name; null leaves the current value.</summary>
    public string? Name { get; set; }

    /// <summary>New description; null leaves the current value.</summary>
    public string? Description { get; set; }

    /// <summary>New SKU; null leaves the current value.</summary>
    public string? Sku { get; set; }

    /// <summary>New barcode; null leaves the current value.</summary>
    public string? Barcode { get; set; }

    /// <summary>New category; null leaves the current value.</summary>
    public string? Category { get; set; }

    /// <summary>New brand; null leaves the current value.</summary>
    public string? Brand { get; set; }

    /// <summary>New selling price; null leaves the current value.</summary>
    public decimal? Price { get; set; }

    /// <summary>New per-unit cost; null leaves the current value.</summary>
    public decimal? Cost { get; set; }

    /// <summary>New stock count; null leaves the current value.</summary>
    public int? Stock { get; set; }

    /// <summary>New taxable flag; null leaves the current value.</summary>
    public bool? IsTaxable { get; set; }

    /// <summary>New tax rate; null leaves the current value.</summary>
    public decimal? TaxRate { get; set; }

    /// <summary>New sellable toggle; null leaves the current value.</summary>
    public bool? IsActive { get; set; }
}
