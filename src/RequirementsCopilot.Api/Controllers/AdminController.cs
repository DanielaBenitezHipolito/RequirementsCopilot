using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RequirementsCopilot.Application.Usage;
using RequirementsCopilot.Application.Users;

namespace RequirementsCopilot.Api.Controllers;

/// <summary>Tablero de control (solo Admin): consumo de tokens del LLM por agente y por día.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = UserAccount.AdminRole)]
public sealed class AdminController : ControllerBase
{
    private readonly ILlmUsageRepository _usage;

    /// <summary>Crea el controlador sobre el repositorio de consumo.</summary>
    public AdminController(ILlmUsageRepository usage) => _usage = usage;

    /// <summary>Agregados de consumo de los últimos <paramref name="dias"/> días (default 30).</summary>
    [HttpGet("usage")]
    public async Task<IActionResult> Usage([FromQuery] int dias = 30, CancellationToken cancellationToken = default)
    {
        DateTime since = DateTime.UtcNow.AddDays(-Math.Clamp(dias, 1, 365));
        IReadOnlyList<LlmUsage> entries = await _usage.GetSinceAsync(since, cancellationToken);
        return Ok(new
        {
            desde = since,
            llamadas = entries.Count,
            tokensEntrada = entries.Sum(e => (long)e.InputTokens),
            tokensSalida = entries.Sum(e => (long)e.OutputTokens),
            porAgente = entries.GroupBy(e => e.Agent)
                .Select(g => new
                {
                    agente = g.Key,
                    llamadas = g.Count(),
                    entrada = g.Sum(e => (long)e.InputTokens),
                    salida = g.Sum(e => (long)e.OutputTokens),
                })
                .OrderByDescending(a => a.entrada + a.salida),
            porDia = entries.GroupBy(e => e.Timestamp.Date)
                .Select(g => new
                {
                    fecha = g.Key.ToString("yyyy-MM-dd"),
                    llamadas = g.Count(),
                    entrada = g.Sum(e => (long)e.InputTokens),
                    salida = g.Sum(e => (long)e.OutputTokens),
                })
                .OrderBy(d => d.fecha),
        });
    }
}
