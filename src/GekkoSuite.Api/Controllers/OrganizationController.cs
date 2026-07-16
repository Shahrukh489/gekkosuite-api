using Microsoft.AspNetCore.Mvc;

namespace GekkoSuite.Api.Controllers;

[ApiController]
[Route("organization")]
public class OrganizationController : ControllerBase
{
    [HttpGet("hello")]
    public IActionResult HelloWorld()
    {
        return Ok("Hello World");
    }
}
