namespace GekkoSuite.Api.Dtos;

public class CreateProductDto
{
    /// <summary>The tenant the store belongs to (from the caller's token).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The store this product is being added at.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The product's display name; null when this row is a variant (see <see cref="GroupId"/> / <see cref="NewGroup"/>).</summary>
    public string? Name { get; set; }

    /// <summary>Optional human description; null when this row is a variant.</summary>
    public string? Description { get; set; }

    /// <summary>This store's own product code; optional.</summary>
    public string? Sku { get; set; }

    /// <summary>Manufacturer barcode; optional.</summary>
    public string? Barcode { get; set; }

    /// <summary>Free-text grouping; null when this row is a variant.</summary>
    public string? Category { get; set; }

    /// <summary>Free-text brand/manufacturer; null when this row is a variant.</summary>
    public string? Brand { get; set; }

    /// <summary>Adds this product as a new variant into an existing group. Mutually exclusive with <see cref="NewGroup"/>.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Creates a new product family and this variant together, atomically. Mutually exclusive with <see cref="GroupId"/>.</summary>
    public CreateProductGroupDto? NewGroup { get; set; }

    /// <summary>This variant's own value along the group's first dimension; only meaningful when grouped.</summary>
    public string? VariantOptionOneValue { get; set; }

    /// <summary>This variant's own value along the group's second dimension; only meaningful when grouped.</summary>
    public string? VariantOptionTwoValue { get; set; }

    /// <summary>This variant's own value along the group's third dimension; only meaningful when grouped.</summary>
    public string? VariantOptionThreeValue { get; set; }

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
