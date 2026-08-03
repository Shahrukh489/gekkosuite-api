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
    /// Get the store details.
    /// Caller must have an store or organization membership and a role with store:read permissions
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}")]
    [EndpointName("GetStoreById")]
    [HasPermission(MembershipScope.STORE, "store:read")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
                return NotFound();
            }

            return Ok(store);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Get the store's users with there memberships
    /// Caller must have an store or organization membership and a role with user:list permissions 
    /// </summary>
    [HttpGet("{" + Constants.STORE_ID + "}/users")]
    [EndpointName("GetStoreUsers")]
    [HasPermission(MembershipScope.STORE, "user:list")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<UserDto>>> GetStoreUsersAsync(Guid storeId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            List<UserDto> users = await _storeService.GetStoreUsersAsync(organizationId, storeId);

            return Ok(users);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }
    // @TODO: add get store user details with memberships information


}
