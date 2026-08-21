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
}
