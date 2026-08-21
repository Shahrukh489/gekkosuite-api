using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class ProductGroupSummaryDto
{
    /// <summary>The group's id.</summary>
    public Guid GroupId { get; set; }

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

    /// <summary>Map from StoreProductGroupEntity to ProductGroupSummaryDto.</summary>
    /// <param name="groupEntity">The store_product_group row to map.</param>
    /// <returns>The client-safe group summary DTO.</returns>
    public static ProductGroupSummaryDto FromEntity(StoreProductGroupEntity groupEntity)
    {
        return new ProductGroupSummaryDto()
        {
            GroupId = groupEntity.GroupId,
            Name = groupEntity.Name,
            Description = groupEntity.Description,
            Category = groupEntity.Category,
            Brand = groupEntity.Brand,
            VariantOptionOneName = groupEntity.VariantOptionOneName,
            VariantOptionTwoName = groupEntity.VariantOptionTwoName,
            VariantOptionThreeName = groupEntity.VariantOptionThreeName,
        };
    }
}
