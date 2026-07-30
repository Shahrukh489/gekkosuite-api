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
    /// Get an organization's details
    /// Caller must have an organization membership and a role with organization:read permissions 
    /// @TODO: look at if the user is org owner how to handle.. does he have a membership?
    /// </summary>
    [HttpGet]
    [EndpointName("GetOrganization")]
    [HasPermission(MembershipScope.ORGANIZATION, "organization:read")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrganizationDto>> GetOrganizationByIdAsync()
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            OrganizationDto? organization = await _organizationService.GetOrganizationByIdAsync(organizationId);
            if (organization is null)
            {
                return NotFound();
            }

            return Ok(organization);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Get all of the users in an organization with there memberships.
    /// Caller must have an organization membership and a role with user:list permissions 
    /// </summary>
    [HttpGet("users")]
    [EndpointName("GetOrganizationUsers")]
    [HasPermission(MembershipScope.ORGANIZATION, "user:list")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<UserDto>>> GetUsersAsync()
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            List<UserDto> users = await _organizationService.GetUsersAsync(organizationId);

            return Ok(users);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }


    /// <summary>
    /// Get all of the roles in an organization.
    /// Caller must have an organization membership and a role with role:list permissions 
    /// </summary>
    [HttpGet("roles")]
    [EndpointName("GetOrganizationRoles")]
    [HasPermission(MembershipScope.ORGANIZATION, "role:list")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<RoleDto>>> GetRolesAsync()
    {
        try
        {
            var organizationId = User.GetOrganizationId();
            
            List<RoleDto> roles = await _organizationService.GetRolesAsync(organizationId);

            return Ok(roles);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }


    /// <summary>
    /// Get the details and permissions of a role
    /// Caller must have an organization membership and a role with role:read permissions 
    /// </summary>
    [HttpGet("roles/{roleId}")]
    [EndpointName("GetOrganizationRoleById")]
    [HasPermission(MembershipScope.ORGANIZATION, "role:read")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RoleDto>> GetRoleByIdAsync(Guid roleId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            RoleDto? role = await _organizationService.GetRoleByIdAsync(organizationId, roleId);
            if (role is null)
            {
                return NotFound();
            }

            return Ok(role);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }


    /// <summary>
    /// Get the stores in an organization.
    /// Caller must have an organization membership and a role with store:list permissions 
    /// </summary>
    [HttpGet("stores")]
    [EndpointName("GetOrganizationStores")]
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

            List<StoreDto> stores = await _organizationService.GetStoresAsync(organizationId);

            return Ok(stores);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }
}
