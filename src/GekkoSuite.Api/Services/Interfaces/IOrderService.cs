using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IOrderService
{
    /// <summary>
    /// Lists the sales orders (receipts) rung up at the given store, newest first.
    /// </summary>
    Task<List<OrderDto>> GetStoreOrdersAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Finds one sales order (receipt) by id at the given store, with its line items, or null if not found.
    /// </summary>
    Task<OrderDto?> GetStoreOrderByIdAsync(Guid organizationId, Guid storeId, Guid orderId);
}
