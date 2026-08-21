namespace GekkoSuite.Api.Entities;

/// <summary>A store's today-at-a-glance summary, derived from its own sales_order rows.</summary>
public class DashboardSummaryEntity
{
    /// <summary>Sum of completed sales made today.</summary>
    public decimal SalesToday { get; set; }

    /// <summary>Count of completed sales made today.</summary>
    public int TransactionsToday { get; set; }

    /// <summary>Total units sold today, across every line of every completed sale.</summary>
    public int ItemsSoldToday { get; set; }

    /// <summary>The store's most recent completed sales (not limited to today), newest first.</summary>
    public List<RecentSaleEntity> RecentSales { get; set; } = [];
}
