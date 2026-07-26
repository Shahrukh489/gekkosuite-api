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
    public async Task<ActionResult<List<StoreProductDto>>> GetStoreProductsAsync(Guid storeId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            List<StoreProductDto> products = await _storeService.GetStoreProductsAsync(organizationId, storeId);

            return Ok(products);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }
}
