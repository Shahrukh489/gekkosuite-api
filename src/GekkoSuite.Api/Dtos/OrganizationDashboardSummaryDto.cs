using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class OrganizationDashboardSummaryDto
{
    /// <summary>Sum of completed sales made today, across every store.</summary>
    public decimal SalesToday { get; set; }

    /// <summary>Count of completed sales made today, across every store.</summary>
    public int TransactionsToday { get; set; }

    /// <summary>Total units sold today, across every line of every completed sale at every store.</summary>
    public int ItemsSoldToday { get; set; }

    /// <summary>The org's most recent completed sales (not limited to today), newest first, across every store.</summary>
    public List<OrgRecentSaleDto> RecentSales { get; set; } = [];

    /// <summary>Map from OrganizationDashboardSummaryEntity to OrganizationDashboardSummaryDto.</summary>
    public static OrganizationDashboardSummaryDto FromEntity(OrganizationDashboardSummaryEntity entity)
    {
        return new OrganizationDashboardSummaryDto()
        {
            SalesToday = entity.SalesToday,
            TransactionsToday = entity.TransactionsToday,
            ItemsSoldToday = entity.ItemsSoldToday,
            RecentSales = entity.RecentSales.Select(OrgRecentSaleDto.FromEntity).ToList(),
        };
    }
}
