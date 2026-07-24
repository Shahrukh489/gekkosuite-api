using Microsoft.AspNetCore.Mvc;

using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Controllers;

[ApiController]
[Route("organization")]
public class OrganizationController : BaseController
{
    private readonly ILogger<OrganizationController> _logger;
    private readonly IOrganizationService _organizationService;

    public OrganizationController(ILogger<OrganizationController> logger, IOrganizationService organizationService) : base(logger)
    {
        _logger = logger;
        _organizationService = organizationService;
    }
}
