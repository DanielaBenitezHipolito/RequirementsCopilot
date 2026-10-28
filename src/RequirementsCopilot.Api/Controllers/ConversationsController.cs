using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Application.Conversations;
using RequirementsCopilot.Application.Projects;

namespace RequirementsCopilot.Api.Controllers;

/// <summary>
/// Entrada conversacional (v2): el usuario describe su idea, el agente entrevistador la convierte
/// en un requerimiento y al completar se inyecta al pipeline normal de análisis.
/// El hilo con Foundry vive en previous_response_id; el historial completo (mensajes, estado)
/// se persiste desde el primer turno para poder listar y reabrir conversaciones.
/// </summary>
[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController : ControllerBase
{
    public sealed record MessageRequest(string? Mensaje, string? PreviousResponseId, Guid? ConversationId,
        string? Proyecto, List<string>? Imagenes = null);

    /// <summary>Tope de imágenes por mensaje (control de costos y de tamaño del payload).</summary>
    private const int MaxImagesPerMessage = 4;

    public sealed record CompleteRequest(string? Texto, string? Area, Guid? ConversationId, string? Proyecto);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private readonly RequirementBuilderAgent _builder;
    private readonly AnalysisOrchestrator _orchestrator;
    private readonly IConversationRepository _conversations;
    private readonly ProjectContextLoader _projectContext;

    public ConversationsController(RequirementBuilderAgent builder, AnalysisOrchestrator orchestrator,
        IConversationRepository conversations, ProjectContextLoader projectContext)
        => (_builder, _orchestrator, _conversations, _projectContext) = (builder, orchestrator, conversations, projectContext);

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] MessageRequest? request, CancellationToken cancellationToken)
    {
        bool hasImages = request?.Imagenes is { Count: > 0 };
        if (string.IsNullOrWhiteSpace(request?.Mensaje) && !hasImages)
        {
            return BadRequest(new { mensaje = "El mensaje no puede estar vacío." });
        }
        if (request!.Imagenes is { Count: > MaxImagesPerMessage })
        {
            return BadRequest(new { mensaje = $"Máximo {MaxImagesPerMessage} imágenes por mensaje." });
        }

        IReadOnlyList<ImageAttachment>? images = null;
        if (hasImages)
        {
            try
            {
                images = request.Imagenes!.Select(ImageAttachment.FromDataUrl).ToArray();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
        }

        string userMessage = string.IsNullOrWhiteSpace(request.Mensaje)
            ? "(Analiza la imagen adjunta.)"
            : request.Mensaje!;

        ConversationRecord? conversation = request.ConversationId is Guid id
            ? await _conversations.GetByIdAsync(id, cancellationToken)
            : null;
        conversation ??= ConversationRecord.Create(request.Proyecto);
        // El historial persiste solo texto; la imagen queda referenciada con un marcador.
        conversation.Append("user", hasImages
            ? $"{userMessage} [+{images!.Count} imagen(es)]"
            : userMessage);

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        try
        {
            // El contexto del proyecto viaja solo en el primer turno; el hilo (previous_response_id) lo conserva.
            string projectContext = request.PreviousResponseId is null
                ? await _projectContext.LoadAsync(conversation.ProjectName, cancellationToken)
                : string.Empty;
            BuilderTurn turn = await _builder.ChatAsync(userMessage, request.PreviousResponseId, projectContext, images, cancellationToken);

            conversation.Append("agent", turn.Mensaje);
            conversation.SetLastResponseId(turn.ResponseId);
            await _conversations.SaveAsync(conversation, cancellationToken);

            await WriteEventAsync("token", new { texto = turn.Mensaje }, cancellationToken);
            if (turn.Listo && turn.Texto is not null)
            {
                await WriteEventAsync("draft", new { requerimiento = new { texto = turn.Texto, area = turn.Area } }, cancellationToken);
            }
            await WriteEventAsync("conversation", new { conversationId = conversation.Id }, cancellationToken);
            await WriteEventAsync("done", new { responseId = turn.ResponseId }, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            await _conversations.SaveAsync(conversation, cancellationToken);
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

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<ConversationRecord> conversations = await _conversations.GetAllAsync(cancellationToken);
        return Ok(conversations.Select(c => new
        {
            id = c.Id,
            createdAt = c.CreatedAt,
            updatedAt = c.UpdatedAt,
            status = c.Status,
            analysisId = c.AnalysisId,
            preview = Preview(c),
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        ConversationRecord? conversation = await _conversations.GetByIdAsync(id, cancellationToken);
        if (conversation is null)
        {
            return NotFound(new { mensaje = "Conversación no encontrada." });
        }

        return Ok(new
        {
            id = conversation.Id,
            status = conversation.Status,
            analysisId = conversation.AnalysisId,
            lastResponseId = conversation.LastResponseId,
            proyecto = conversation.ProjectName,
            messages = conversation.Messages.Select(m => new { role = m.Role, text = m.Text }),
        });
    }

    private static string Preview(ConversationRecord conversation)
    {
        string? firstUserMessage = conversation.Messages.FirstOrDefault(m => m.Role == "user")?.Text;
        if (string.IsNullOrEmpty(firstUserMessage))
        {
            return string.Empty;
        }
        return firstUserMessage.Length > 80 ? firstUserMessage[..80] + "…" : firstUserMessage;
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
            Guid analysisId = await _orchestrator.CreateFromRequirementAsync(request.Texto, request.Area,
                request.Proyecto, cancellationToken);

            if (request.ConversationId is Guid conversationId)
            {
                ConversationRecord? conversation = await _conversations.GetByIdAsync(conversationId, cancellationToken);
                if (conversation is not null)
                {
                    conversation.Complete(analysisId);
                    await _conversations.SaveAsync(conversation, cancellationToken);
                }
            }

            return Ok(new { analysisId });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }
}
