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
    private readonly IRoleService _roleService;

    public StoreController(ILogger<StoreController> logger, IStoreService storeService, IRoleService roleService) : base(logger)
    {
        _logger = logger;
        _storeService = storeService;
        _roleService = roleService;
    }

    /// <summary>
    /// Lists the stores in the caller's organization (the org's roster).
    /// </summary>
    [HttpGet]
    [HasPermission(MembershipScope.ORGANIZATION, "store:list")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<StoreDto>>> GetStoresAsync()
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            List<StoreDto> stores = await _storeService.GetStoresAsync(organizationId);

            return Ok(stores);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Returns a store's record and its store-scoped features. The store must belong to the caller's org.
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}")]
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

            List<RoleDto> roles = await _roleService.GetRolesAsync(organizationId, MembershipScope.STORE);

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

            RoleDto? role = await _roleService.GetRoleByIdAsync(organizationId, roleId, MembershipScope.STORE);
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
}
