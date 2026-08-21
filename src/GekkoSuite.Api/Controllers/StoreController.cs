using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

using GekkoSuite.Api;
using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Extensions;
using GekkoSuite.Api.Policies;
using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Controllers;

[ApiController]
[Route("stores")]
public class StoreController : BaseController
{
    private readonly ILogger<StoreController> _logger;
    private readonly IStoreService _storeService;

    public StoreController(ILogger<StoreController> logger, IStoreService storeService) : base(logger)
    {
        _logger = logger;
        _storeService = storeService;
    }

    /// <summary>
    /// Returns a store's record and its store-scoped features. The store must belong to the caller's org.
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}")]
    [EndpointName("GetStoreById")]
    [HasPermission(MembershipScope.STORE, "store:read")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StoreDto>> GetStoreByIdAsync(Guid storeId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            StoreDto? store = await _storeService.GetStoreByIdAsync(organizationId, storeId);
            if (store is null)
            {
                return NotFound(new { message = "Store not found." });
            }

            return Ok(store);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Summarizes a store's day for its dashboard — today's sales/transactions/items and its most
    /// recent completed sales, derived from the store's own sales_order rows.
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}/dashboard")]
    [EndpointName("GetStoreDashboardSummary")]
    [HasPermission(MembershipScope.STORE, "dashboard:read")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DashboardSummaryDto>> GetStoreDashboardSummaryAsync(Guid storeId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            DashboardSummaryDto summary = await _storeService.GetStoreDashboardSummaryAsync(organizationId, storeId);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Lists the staff roster at a store — the users with a membership there, each with their store role(s).
    /// The store must belong to the caller's org.
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}/users")]
    [EndpointName("GetStoreUsers")]
    [HasPermission(MembershipScope.STORE, "user:list")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<UserDto>>> GetStoreUsersByStoreIdAsync(Guid storeId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            List<UserDto> users = await _storeService.GetStoreUsersByStoreIdAsync(organizationId, storeId);

            return Ok(users);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Lists the STORE-scoped roles assignable in the caller's org, for a store the caller belongs to.
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}/roles")]
    [EndpointName("GetStoreRoles")]
    [HasPermission(MembershipScope.STORE, "role:list")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<RoleDto>>> GetStoreRolesAsync(Guid storeId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            List<RoleDto> roles = await _storeService.GetStoreRolesAsync(organizationId);

            return Ok(roles);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Returns one role and the permissions it grants, in the context of a store the caller belongs to.
    /// The store must belong to the caller's org.
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}/roles/{roleId}")]
    [EndpointName("GetStoreRoleById")]
    [HasPermission(MembershipScope.STORE, "role:read")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RoleDto>> GetStoreRoleByIdAsync(Guid storeId, Guid roleId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            RoleDto? role = await _storeService.GetStoreRoleByIdAsync(organizationId, roleId);
            if (role is null)
            {
                return NotFound(new { message = "Role not found." });
            }

            return Ok(role);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Lists the products at a store — the store's own catalog with its per-store stock and price.
    /// The store must belong to the caller's org.
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}/products")]
    [EndpointName("GetStoreProducts")]
    [HasPermission(MembershipScope.STORE, "product:list")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ProductDto>>> GetStoreProductsAsync(Guid storeId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            List<ProductDto> products = await _storeService.GetStoreProductsAsync(organizationId, storeId);

            return Ok(products);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Creates a product at a store the caller belongs to. There is no dual-write to a shared, org-wide
    /// product identity — that toggle (organization.allow_share_products) doesn't exist in the schema
    /// yet (see docs/product.md), so this always writes store_product only.
    /// </summary>
    [HttpPost("{" + Constants.STORE_ID + "}/products")]
    [EndpointName("CreateStoreProduct")]
    [HasPermission(MembershipScope.STORE, "product:create")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductDto>> CreateStoreProductAsync(Guid storeId, CreateProductRequest request)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            ProductDto product = await _storeService.CreateStoreProductAsync(organizationId, storeId, request);

            return Ok(product);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Updates the given fields on a live product at a store the caller belongs to (unset fields are
    /// left unchanged).
    /// </summary>
    [HttpPatch("{" + Constants.STORE_ID + "}/products/{productId}")]
    [EndpointName("UpdateStoreProduct")]
    [HasPermission(MembershipScope.STORE, "product:edit")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductDto>> UpdateStoreProductAsync(Guid storeId, Guid productId, UpdateProductRequest request)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            ProductDto? product = await _storeService.UpdateStoreProductAsync(organizationId, storeId, productId, request);
            if (product is null)
            {
                return NotFound(new { message = "Product not found." });
            }

            return Ok(product);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Updates the given fields on a live product group (variant family) at a store the caller belongs
    /// to (unset fields are left unchanged). Reuses product:edit — a group is never created or deleted
    /// on its own, only renamed alongside the products it already owns.
    /// </summary>
    [HttpPatch("{" + Constants.STORE_ID + "}/product-groups/{groupId}")]
    [EndpointName("UpdateStoreProductGroup")]
    [HasPermission(MembershipScope.STORE, "product:edit")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductGroupSummaryDto>> UpdateStoreProductGroupAsync(Guid storeId, Guid groupId, UpdateProductGroupRequest request)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            ProductGroupSummaryDto? group = await _storeService.UpdateStoreProductGroupAsync(organizationId, storeId, groupId, request);
            if (group is null)
            {
                return NotFound(new { message = "Product group not found." });
            }

            return Ok(group);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Lists the customers at a store — the store's own roster with contact details.
    /// The store must belong to the caller's org.
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}/customers")]
    [EndpointName("GetStoreCustomers")]
    [HasPermission(MembershipScope.STORE, "customer:list")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CustomerDto>>> GetStoreCustomersAsync(Guid storeId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            List<CustomerDto> customers = await _storeService.GetStoreCustomersAsync(organizationId, storeId);

            return Ok(customers);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Creates a customer at a store the caller belongs to.
    /// </summary>
    [HttpPost("{" + Constants.STORE_ID + "}/customers")]
    [EndpointName("CreateStoreCustomer")]
    [HasPermission(MembershipScope.STORE, "customer:create")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CustomerDto>> CreateStoreCustomerAsync(Guid storeId, CreateCustomerRequest request)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            CustomerDto customer = await _storeService.CreateStoreCustomerAsync(organizationId, storeId, request);

            return Ok(customer);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Lists the sales orders (receipts) rung up at a store, newest first. The store must belong to the caller's org.
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}/orders")]
    [EndpointName("GetStoreOrders")]
    [HasPermission(MembershipScope.STORE, "order:list")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<OrderDto>>> GetStoreOrdersAsync(Guid storeId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            List<OrderDto> orders = await _storeService.GetStoreOrdersAsync(organizationId, storeId);

            return Ok(orders);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Rings up a sale at a store the caller belongs to: prices and taxes the cart server-side from the
    /// store's live catalog, writes the order, decrements stock, and returns the created receipt.
    /// </summary>
    [HttpPost("{" + Constants.STORE_ID + "}/orders")]
    [EndpointName("CreateStoreOrder")]
    [HasPermission(MembershipScope.STORE, "sales_order:create")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderDto>> CreateStoreOrderAsync(Guid storeId, CreateOrderRequest request)
    {
        try
        {
            var organizationId = User.GetOrganizationId();
            var soldByUserId = User.GetUserId();

            OrderDto order = await _storeService.CreateStoreOrderAsync(organizationId, storeId, soldByUserId, request);

            return Ok(order);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Returns one sales order (receipt) and its line items, at a store the caller belongs to.
    /// The store must belong to the caller's org.
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}/orders/{orderId}")]
    [EndpointName("GetStoreOrderById")]
    [HasPermission(MembershipScope.STORE, "order:read")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderDto>> GetStoreOrderByIdAsync(Guid storeId, Guid orderId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            OrderDto? order = await _storeService.GetStoreOrderByIdAsync(organizationId, storeId, orderId);
            if (order is null)
            {
                return NotFound(new { message = "Order not found." });
            }

            return Ok(order);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }
}
