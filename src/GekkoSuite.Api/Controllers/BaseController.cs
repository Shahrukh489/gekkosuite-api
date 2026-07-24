using Microsoft.AspNetCore.Mvc;

namespace GekkoSuite.Api.Controllers;

public class BaseController : ControllerBase
{
    private readonly ILogger<BaseController> _logger;

    public BaseController(ILogger<BaseController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Map the exception a return code
    /// </summary>
    public ActionResult ErrorResponse(Exception ex)
    {
        _logger.LogError(ex, "Unexpected error.");
        return StatusCode(StatusCodes.Status500InternalServerError);
    }
}