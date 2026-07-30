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
    /// Lists the users in the caller's organization, each with their memberships (role names and details).
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
    /// Returns one user in the caller's organization — their record plus their memberships (org or store).
    /// </summary>
    [HttpGet("users/{userId}")]
    [EndpointName("GetOrganizationUserById")]
    [HasPermission(MembershipScope.ORGANIZATION, "user:read")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDto>> GetUserByIdAsync(Guid userId)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            UserDto? user = await _organizationService.GetUserByIdAsync(organizationId, userId);
            if (user is null)
            {
                return NotFound();
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Lists the roles assignable in the caller's org — managed roles plus the org's own custom roles.
    /// Pass ?scope=ORGANIZATION or ?scope=STORE to return only roles valid for that membership kind.
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
    /// Returns one role and the permissions it grants. Visible only if it is managed or owned by the caller's org.
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
    /// Lists the stores in the caller's organization (the org's roster).
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
