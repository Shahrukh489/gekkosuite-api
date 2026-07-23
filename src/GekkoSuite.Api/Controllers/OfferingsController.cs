using Microsoft.AspNetCore.Mvc;

using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Controllers;

[ApiController]
public class OfferingsController : ControllerBase
{
    private readonly IOfferingService _offeringService;

    public OfferingsController(IOfferingService offeringService)
    {
        _offeringService = offeringService;
    }

    /// <summary>
    /// Lists the available base plans for the signup page (docs/api.md). Public — no token required.
    /// </summary>
    /// <returns>200 with the active plan offerings.</returns>
    [HttpGet("/plans")]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _offeringService.GetPlansAsync();
        return Ok(plans);
    }

    /// <summary>
    /// Lists the available add-ons (stackable extras) (docs/api.md). Public — no token required.
    /// </summary>
    /// <returns>200 with the active add-on offerings.</returns>
    [HttpGet("/addons")]
    public async Task<IActionResult> GetAddons()
    {
        var addons = await _offeringService.GetAddonsAsync();
        return Ok(addons);
    }
}
