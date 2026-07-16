using Microsoft.AspNetCore.Mvc;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Analyses.Agents;

namespace RequirementsCopilot.Api.Controllers;

/// <summary>
/// Entrada conversacional (v2): el usuario describe su idea, el agente entrevistador la convierte
/// en un requerimiento y al completar se inyecta al pipeline normal de análisis.
/// El estado del hilo vive en Foundry (previous_response_id); el servidor no guarda conversaciones.
/// </summary>
[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController : ControllerBase
{
    public sealed record MessageRequest(string? Mensaje, string? PreviousResponseId);

    public sealed record CompleteRequest(string? Texto, string? Area);

    private readonly RequirementBuilderAgent _builder;
    private readonly AnalysisOrchestrator _orchestrator;

    public ConversationsController(RequirementBuilderAgent builder, AnalysisOrchestrator orchestrator)
        => (_builder, _orchestrator) = (builder, orchestrator);

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] MessageRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Mensaje))
        {
            return BadRequest(new { mensaje = "El mensaje no puede estar vacío." });
        }

        try
        {
            BuilderTurn turn = await _builder.ChatAsync(request.Mensaje, request.PreviousResponseId, cancellationToken);
            return Ok(new
            {
                mensaje = turn.Mensaje,
                listo = turn.Listo,
                requerimiento = turn.Listo ? new { texto = turn.Texto, area = turn.Area } : null,
                previousResponseId = turn.ResponseId,
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] CompleteRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Texto))
        {
            return BadRequest(new { mensaje = "Debe enviar el texto del requerimiento." });
        }

        try
        {
            Guid analysisId = await _orchestrator.CreateFromRequirementAsync(request.Texto, request.Area, cancellationToken);
            return Ok(new { analysisId });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }
}
