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
    [HasPermission(MembershipScope.STORE, "store:read")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StoreDto>> GetStoreAsync(Guid storeId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            StoreDto? store = await _storeService.GetStoreAsync(organizationId, storeId);
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
}
