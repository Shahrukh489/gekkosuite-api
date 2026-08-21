namespace GekkoSuite.Api.Dtos;

/// <summary>
/// Nested inside <see cref="CreateProductRequest"/> to create a new product family together with its
/// first variant, in one call — a group is never created empty. There is no standalone endpoint for
/// creating a group on its own.
/// </summary>
public class CreateProductGroupRequest
{
    /// <summary>The product family's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human description.</summary>
    public string? Description { get; set; }

    /// <summary>Free-text grouping, e.g. "Beverages"; optional.</summary>
    public string? Category { get; set; }

    /// <summary>Free-text brand/manufacturer; optional.</summary>
    public string? Brand { get; set; }

    /// <summary>The first variant dimension name this product varies along, e.g. "Flavor"; optional.</summary>
    public string? VariantOptionOneName { get; set; }

    /// <summary>The second variant dimension name; optional.</summary>
    public string? VariantOptionTwoName { get; set; }

    /// <summary>The third variant dimension name; optional.</summary>
    public string? VariantOptionThreeName { get; set; }
}
