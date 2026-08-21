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

    /// <summary>
    /// Rings up a sale: validates the cart against the store's live catalog, prices and taxes it
    /// server-side, writes the order and decrements stock, and returns the created receipt. Throws
    /// BadRequestException for an empty cart, an invalid quantity, a product not sellable at this
    /// store, or a customer not at this store.
    /// </summary>
    Task<OrderDto> CreateOrderAsync(Guid organizationId, Guid storeId, Guid soldByUserId, CreateOrderRequest request);
}
