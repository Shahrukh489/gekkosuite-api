using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Dtos;

public class DashboardSummaryDto
{
    /// <summary>Sum of completed sales made today.</summary>
    public decimal SalesToday { get; set; }

    /// <summary>Count of completed sales made today.</summary>
    public int TransactionsToday { get; set; }

    /// <summary>Total units sold today, across every line of every completed sale.</summary>
    public int ItemsSoldToday { get; set; }

    /// <summary>The store's most recent completed sales (not limited to today), newest first.</summary>
    public List<RecentSaleDto> RecentSales { get; set; } = [];

    /// <summary>Map from DashboardSummaryEntity to DashboardSummaryDto.</summary>
    public static DashboardSummaryDto FromEntity(DashboardSummaryEntity entity)
    {
        return new DashboardSummaryDto()
        {
            SalesToday = entity.SalesToday,
            TransactionsToday = entity.TransactionsToday,
            ItemsSoldToday = entity.ItemsSoldToday,
            RecentSales = entity.RecentSales.Select(RecentSaleDto.FromEntity).ToList(),
        };
    }
}
