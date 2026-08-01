using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Controllers;

[ApiController]
[Route("offerings")]
public class OfferingController : BaseController
{
    private readonly ILogger<OfferingController> _logger;
    private readonly IOfferingService _offeringService;

    public OfferingController(ILogger<OfferingController> logger, IOfferingService offeringService) : base(logger)
    {
        _logger = logger;
        _offeringService = offeringService;
    }

    /// <summary>
    /// Get all the available offerings
    /// Public endpoint without any permissions needed
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<OfferingDto>>> GetOfferingsAsync()
    {
        try
        {
            List<OfferingDto> offerings = await _offeringService.GetOfferingsAsync();

            return Ok(offerings);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }
}
