namespace GekkoSuite.Api.Entities;

/// <summary>One row in a store dashboard's "recent sales" feed — a lighter-weight sibling of
/// OrderEntity with no line-item detail, just enough to list a sale.</summary>
public class RecentSaleEntity
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
}
