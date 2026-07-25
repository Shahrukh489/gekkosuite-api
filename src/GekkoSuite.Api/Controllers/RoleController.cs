using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Extensions;
using GekkoSuite.Api.Policies;
using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Controllers;

[ApiController]
[Route("roles")]
public class RoleController : BaseController
{
    private readonly ILogger<RoleController> _logger;
    private readonly IRoleService _roleService;

    public RoleController(ILogger<RoleController> logger, IRoleService roleService) : base(logger)
    {
        _logger = logger;
        _roleService = roleService;
    }

    /// <summary>
    /// Lists the roles assignable in the caller's org — managed roles plus the org's own custom roles.
    /// Pass ?scope=ORGANIZATION or ?scope=STORE to return only roles valid for that membership kind.
    /// </summary>
    [HttpGet]
    [HasPermission(MembershipScope.ORGANIZATION, "role:read")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<RoleDto>>> GetRolesAsync([FromQuery] string? scope)
    {
        try
        {
            var organizationId = User.GetOrganizationId();

            MembershipScope? parsedScope = null;
            if (!string.IsNullOrEmpty(scope))
            {
                if (!Enum.TryParse<MembershipScope>(scope, out var value))
                {
                    return BadRequest(new { message = "Invalid scope. Use ORGANIZATION or STORE." });
                }

                parsedScope = value;
            }

            List<RoleDto> roles = await _roleService.GetRolesAsync(organizationId, parsedScope);

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
    [HttpGet("{roleId}")]
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

            RoleDto? role = await _roleService.GetRoleByIdAsync(organizationId, roleId);
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
