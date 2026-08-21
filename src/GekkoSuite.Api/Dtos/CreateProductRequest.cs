namespace GekkoSuite.Api.Dtos;

public class CreateProductRequest
{
    /// <summary>
    /// The product's display name. Required and used only when neither <see cref="GroupId"/> nor
    /// <see cref="NewGroup"/> is set (a standalone product). Must be omitted otherwise — a variant
    /// defers naming to its group.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Optional human description. Same rule as <see cref="Name"/>: omit when grouped.</summary>
    public string? Description { get; set; }

    /// <summary>This store's own product code; optional, unique per store.</summary>
    public string? Sku { get; set; }

    /// <summary>Manufacturer barcode (UPC/EAN); optional, unique per store.</summary>
    public string? Barcode { get; set; }

    /// <summary>Free-text grouping, e.g. "Beverages". Same rule as <see cref="Name"/>: omit when grouped.</summary>
    public string? Category { get; set; }

    /// <summary>Free-text brand/manufacturer. Same rule as <see cref="Name"/>: omit when grouped.</summary>
    public string? Brand { get; set; }

    /// <summary>Adds this product as a new variant into an existing group. Mutually exclusive with <see cref="NewGroup"/>.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Creates a new product family and this variant together, atomically. Mutually exclusive with <see cref="GroupId"/>.</summary>
    public CreateProductGroupRequest? NewGroup { get; set; }

    /// <summary>This variant's own value along the group's first dimension, e.g. "Vanilla"; only meaningful when grouped.</summary>
    public string? VariantOptionOneValue { get; set; }

    /// <summary>This variant's own value along the group's second dimension; only meaningful when grouped.</summary>
    public string? VariantOptionTwoValue { get; set; }

    /// <summary>This variant's own value along the group's third dimension; only meaningful when grouped.</summary>
    public string? VariantOptionThreeValue { get; set; }

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
