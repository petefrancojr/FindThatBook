using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Features.Health.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    /// <summary>Reports that the API is running.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new { status = "ok!" });
}
