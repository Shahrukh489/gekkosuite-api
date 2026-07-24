using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using GekkoSuite.Api.Extensions;
using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Controllers;


[ApiController]
[Route("user")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserService userService, ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the current user (self read). The userId and organizationId are taken only from the
    /// validated token, never from the request. Any authenticated user may call this; no permission needed.
    /// </summary>
    [Authorize]
    [HttpGet("")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserAsync()
    {
        try
        {
            var userId = User.GetUserId();
            var organizationId = User.GetOrganizationId();

            var user = await _userService.GetUserAsync(organizationId, userId);
            if (user is null)
            {
                return NotFound();
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
