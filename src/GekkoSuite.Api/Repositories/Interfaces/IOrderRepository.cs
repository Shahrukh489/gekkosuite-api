using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public interface IOrderRepository
{
    /// <summary>
    /// Lists the sales orders (receipts) rung up at the given store, newest first. Lines are not included.
    /// </summary>
    public Task<IEnumerable<OrderEntity>> GetStoreOrdersAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Finds one sales order (receipt) by id at the given store, with its line items, or null if not found.
    /// </summary>
    public Task<OrderEntity?> GetStoreOrderByIdAsync(Guid organizationId, Guid storeId, Guid orderId);

    /// <summary>
    /// Writes a completed sale in one statement: assigns the next per-store order number, inserts the
    /// order and its lines, and decrements stock for every tracked product sold — so a partial failure
    /// can't leave a receipt with missing lines or untouched inventory.
    /// </summary>
    public Task CreateOrderAsync(CreateOrderDto dto, DateTimeOffset now);

    /// <summary>
    /// Summarizes the store's day: today's completed sales/transactions/items, plus its most recent
    /// completed sales (not limited to today, so a slow day doesn't show an empty feed).
    /// </summary>
    public Task<DashboardSummaryEntity> GetStoreDashboardSummaryAsync(Guid organizationId, Guid storeId);
}
