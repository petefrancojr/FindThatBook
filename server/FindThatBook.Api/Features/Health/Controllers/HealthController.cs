using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Features.Health.Controllers;

[ApiController]
[Route("api/health")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    /// <summary>Reports that the API is running.</summary>
    [HttpGet(Name = "GetHealth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new { status = "ok!" });
}
