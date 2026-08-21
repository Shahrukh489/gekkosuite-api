namespace GekkoSuite.Api.Entities;

/// <summary>One row in the organization dashboard's "recent sales" feed — spans every store in the
/// org, so unlike RecentSaleEntity it also names which store the sale was rung up at.</summary>
public class OrgRecentSaleEntity
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
}
