using Microsoft.AspNetCore.Mvc;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    // GET /api/health
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "ok", timestamp = DateTime.UtcNow });
    }
}
