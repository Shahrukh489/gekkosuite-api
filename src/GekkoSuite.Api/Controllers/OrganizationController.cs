using Microsoft.AspNetCore.Mvc;

using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Controllers;

[ApiController]
[Route("organization")]
public class OrganizationController : ControllerBase
{
    private readonly ILogger<OrganizationController> _logger;
    private readonly IOrganizationService _organizationService;

    public OrganizationController(ILogger<OrganizationController> logger, IOrganizationService organizationService)
    {
        _logger = logger;
        _organizationService = organizationService;
    }

    [HttpGet("")]
    public IActionResult HelloWorld()
    {
        _logger.LogDebug("in controller");
        return Ok(_organizationService.GetGreeting());
    }

    [HttpGet("db-version")]
    public async Task<IActionResult> GetDatabaseVersion([FromQuery] Guid organizationId)
    {
        // TODO: organizationId will come from the validated JWT, not a query param.
        var version = await _organizationService.GetDatabaseVersionAsync(organizationId);
        return Ok(version);
    }
}
