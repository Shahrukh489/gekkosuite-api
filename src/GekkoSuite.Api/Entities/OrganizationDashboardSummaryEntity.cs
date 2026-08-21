namespace GekkoSuite.Api.Entities;

/// <summary>An org's today-at-a-glance summary — the same shape as a store's, but rolled up across
/// every store in the org.</summary>
public class OrganizationDashboardSummaryEntity
{
    /// <summary>Sum of completed sales made today, across every store.</summary>
    public decimal SalesToday { get; set; }

    /// <summary>Count of completed sales made today, across every store.</summary>
    public int TransactionsToday { get; set; }

    /// <summary>Total units sold today, across every line of every completed sale at every store.</summary>
    public int ItemsSoldToday { get; set; }

    /// <summary>The org's most recent completed sales (not limited to today), newest first, across every store.</summary>
    public List<OrgRecentSaleEntity> RecentSales { get; set; } = [];
}
