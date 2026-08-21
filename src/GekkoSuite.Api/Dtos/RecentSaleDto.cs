using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class RecentSaleDto
{
    /// <summary>The order's id.</summary>
    public Guid OrderId { get; set; }

    /// <summary>Human-readable receipt number.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>When the sale was made (stored UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Total units sold across every line on the order.</summary>
    public int ItemCount { get; set; }

    /// <summary>What the customer paid.</summary>
    public decimal Total { get; set; }

    /// <summary>Map from RecentSaleEntity to RecentSaleDto.</summary>
    public static RecentSaleDto FromEntity(RecentSaleEntity entity)
    {
        return new RecentSaleDto()
        {
            OrderId = entity.OrderId,
            OrderNumber = entity.OrderNumber,
            CreatedAt = entity.CreatedAt,
            ItemCount = entity.ItemCount,
            Total = entity.Total,
        };
    }
}
