using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

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

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        try
        {
            BuilderTurn turn = await _builder.ChatAsync(request.Mensaje, request.PreviousResponseId, cancellationToken);

            await WriteEventAsync("token", new { texto = turn.Mensaje }, cancellationToken);
            if (turn.Listo && turn.Texto is not null)
            {
                await WriteEventAsync("draft", new { requerimiento = new { texto = turn.Texto, area = turn.Area } }, cancellationToken);
            }
            await WriteEventAsync("done", new { responseId = turn.ResponseId }, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            await WriteEventAsync("error", new { mensaje = ex.Message }, cancellationToken);
        }

        return new EmptyResult();
    }

    private async Task WriteEventAsync(string name, object payload, CancellationToken cancellationToken)
    {
        string data = JsonSerializer.Serialize(payload, JsonOptions);
        await Response.WriteAsync($"event: {name}\ndata: {data}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
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
