using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Services;

public interface IStoreService
{
    /// <summary>
    /// Lists the users with a store membership at the given store (the staff roster), each with their membership(s).
    /// </summary>
    Task<List<UserDto>> GetStoreUsersByStoreIdAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Lists the stores in the given organization (the org's roster), default store first.
    /// </summary>
    Task<List<StoreDto>> GetStoresAsync(Guid organizationId);

    /// <summary>
    /// Finds a store by id within the given organization (with its store-scoped features), or null if it isn't in that org.
    /// </summary>
    Task<StoreDto?> GetStoreByIdAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Lists the STORE-scoped roles assignable in the org (managed + the org's own), for a store the caller belongs to.
    /// </summary>
    Task<List<RoleDto>> GetStoreRolesAsync(Guid organizationId);

    /// <summary>
    /// Returns one STORE-scoped role and the permissions it grants, or null if not visible to the org or not a store role.
    /// </summary>
    Task<RoleDto?> GetStoreRoleByIdAsync(Guid organizationId, Guid roleId);

    /// <summary>
    /// Lists the products at the given store — the store's own catalog with its per-store stock and price.
    /// </summary>
    Task<List<ProductDto>> GetStoreProductsAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Lists the customers at the given store — the store's own roster with contact details.
    /// </summary>
    Task<List<CustomerDto>> GetStoreCustomersAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Lists the sales orders (receipts) rung up at the given store, newest first.
    /// </summary>
    Task<List<OrderDto>> GetStoreOrdersAsync(Guid organizationId, Guid storeId);

    /// <summary>
    /// Finds one sales order (receipt) by id at the given store, with its line items, or null if not found.
    /// </summary>
    Task<OrderDto?> GetStoreOrderByIdAsync(Guid organizationId, Guid storeId, Guid orderId);

    /// <summary>
    /// Rings up a sale at the given store and returns the created receipt.
    /// </summary>
    Task<OrderDto> CreateStoreOrderAsync(Guid organizationId, Guid storeId, Guid soldByUserId, CreateOrderRequest request);

    /// <summary>
    /// Creates a customer at the given store.
    /// </summary>
    Task<CustomerDto> CreateStoreCustomerAsync(Guid organizationId, Guid storeId, CreateCustomerRequest request);

    /// <summary>
    /// Summarizes the given store's day for its dashboard.
    /// </summary>
    Task<DashboardSummaryDto> GetStoreDashboardSummaryAsync(Guid organizationId, Guid storeId);
}
