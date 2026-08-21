namespace GekkoSuite.Api.Entities;

public class StoreProductGroupEntity
{
    /// <summary>The group's id.</summary>
    public Guid GroupId { get; set; }

    /// <summary>The store this product family belongs to.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The organization that owns the store (the tenant).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The product family's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human description of the product family.</summary>
    public string? Description { get; set; }

    /// <summary>Free-text grouping for browsing/filtering; null if unset.</summary>
    public string? Category { get; set; }

    /// <summary>Free-text brand/manufacturer; null if unset.</summary>
    public string? Brand { get; set; }

    /// <summary>The first variant dimension name this product varies along, e.g. "Flavor"; null if unused.</summary>
    public string? VariantOptionOneName { get; set; }

    /// <summary>The second variant dimension name; null if unused.</summary>
    public string? VariantOptionTwoName { get; set; }

    /// <summary>The third variant dimension name; null if unused.</summary>
    public string? VariantOptionThreeName { get; set; }

    /// <summary>When the group was created (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the group was last modified (stored UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
