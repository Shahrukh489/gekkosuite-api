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
