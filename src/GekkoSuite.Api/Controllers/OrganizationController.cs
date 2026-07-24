using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Extensions;
using GekkoSuite.Api.Policies;
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

    /// <summary>
    /// Returns the caller's organization record. The organizationId is taken from the validated token.
    /// </summary>
    [HttpGet("")]
    [HasPermission(MembershipScope.ORGANIZATION, "organization:read")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrganizationDto>> GetOrganizationAsync()
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            OrganizationDto? organization = await _organizationService.GetOrganizationByIdAsync(organizationId);
            if (organization is null)
            {
                return NotFound(new { message = "Organization not found." });
            }

            return Ok(organization);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }
}
