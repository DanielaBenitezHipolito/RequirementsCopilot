using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RequirementsCopilot.Api.Controllers;

/// <summary>Health check anónimo (Render lo consulta aun con la autenticación habilitada).</summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Latido del servicio.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new { estado = "ok" });
}
