using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class OrgRecentSaleDto
{
    /// <summary>The order's id.</summary>
    public Guid OrderId { get; set; }

    /// <summary>Human-readable receipt number.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>The store the sale was rung up at.</summary>
    public Guid StoreId { get; set; }

    /// <summary>The store's display name.</summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>When the sale was made (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Total units sold across every line on the order.</summary>
    public int ItemCount { get; set; }

    /// <summary>What the customer paid.</summary>
    public decimal Total { get; set; }

    /// <summary>Map from OrgRecentSaleEntity to OrgRecentSaleDto.</summary>
    public static OrgRecentSaleDto FromEntity(OrgRecentSaleEntity entity)
    {
        return new OrgRecentSaleDto()
        {
            OrderId = entity.OrderId,
            OrderNumber = entity.OrderNumber,
            StoreId = entity.StoreId,
            StoreName = entity.StoreName,
            CreatedAt = entity.CreatedAt,
            ItemCount = entity.ItemCount,
            Total = entity.Total,
        };
    }
}
