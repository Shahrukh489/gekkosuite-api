using Microsoft.AspNetCore.Mvc;

using GekkoSuite.Api.Exceptions;

namespace GekkoSuite.Api.Controllers;

public class BaseController : ControllerBase
{
    private readonly ILogger<BaseController> _logger;

    public BaseController(ILogger<BaseController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Map the exception a return code. BadRequestException and ConflictException are client-input
    /// errors a service rejects on purpose, so their message is safe to return; everything else
    /// (including ValidationException, which marks a server-side invariant) is logged and hidden
    /// behind a generic 500.
    /// </summary>
    public ActionResult ErrorResponse(Exception ex)
    {
        if (ex is BadRequestException badRequestException)
        {
            return BadRequest(new { message = badRequestException.Message });
        }

        if (ex is ConflictException conflictException)
        {
            return Conflict(new { message = conflictException.Message });
        }

        _logger.LogError(ex, "Unexpected error.");
        return StatusCode(StatusCodes.Status500InternalServerError);
    }
}