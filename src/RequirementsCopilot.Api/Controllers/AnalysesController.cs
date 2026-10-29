using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public sealed class AnalysesController : ControllerBase
{
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedExtensions =
        { ".pdf", ".docx", ".txt", ".md", ".png", ".jpg", ".jpeg", ".webp", ".gif" };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private readonly AnalysisOrchestrator _orchestrator;
    private readonly AnalysisQueries _queries;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly IAnalysisRepository _repository;

    public AnalysesController(AnalysisOrchestrator orchestrator, AnalysisQueries queries,
        IDocumentTextExtractor textExtractor, IAnalysisRepository repository)
        => (_orchestrator, _queries, _textExtractor, _repository) = (orchestrator, queries, textExtractor, repository);

    [HttpPost]
    [RequestSizeLimit(MaxFileBytes + 1024)]
    public async Task<IActionResult> Analyze(IFormFile? file, [FromForm] string? proyecto, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { mensaje = "Debe adjuntar un archivo." });
        }
        if (file.Length > MaxFileBytes)
        {
            return BadRequest(new { mensaje = "El archivo supera el máximo de 10 MB." });
        }
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(new { mensaje = $"Formato no soportado: {extension}. Use PDF, DOCX, TXT, MD o una imagen (PNG, JPG, WEBP, GIF)." });
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        await using Stream content = file.OpenReadStream();
        await foreach (AnalysisEvent analysisEvent in _orchestrator.AnalyzeAsync(content, file.FileName, proyecto, cancellationToken))
        {
            await WriteEventAsync(analysisEvent, cancellationToken);
        }
        return new EmptyResult();
    }

    /// <summary>Extrae el texto plano de un documento para previsualizarlo/editarlo antes de auditar.</summary>
    [HttpPost("/api/documents/extract")]
    [RequestSizeLimit(MaxFileBytes + 1024)]
    public async Task<IActionResult> ExtractText(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { mensaje = "Debe adjuntar un archivo." });
        }
        if (file.Length > MaxFileBytes)
        {
            return BadRequest(new { mensaje = "El archivo supera el máximo de 10 MB." });
        }
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(new { mensaje = $"Formato no soportado: {extension}. Use PDF, DOCX, TXT, MD o una imagen (PNG, JPG, WEBP, GIF)." });
        }

        await using Stream content = file.OpenReadStream();
        try
        {
            DocumentContent contenido = await _textExtractor.ExtractAsync(content, file.FileName, cancellationToken);
            return Ok(new { texto = contenido.Text, imagenes = contenido.Images.Count });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BadRequest(new { mensaje = $"No se pudo leer el documento: {ex.Message}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await _queries.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        AnalysisDetailDto? detail = await _queries.GetByIdAsync(id, cancellationToken);
        return detail is null ? NotFound(new { mensaje = "Análisis no encontrado." }) : Ok(detail);
    }

    /// <summary>
    /// Elimina un análisis completo. Sus casos de uso dejan de alimentar el contexto del proyecto
    /// (la memoria es derivada, ADR-0009).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => await _repository.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : NotFound(new { mensaje = "Análisis no encontrado." });

    public sealed record ClarificationAnswersRequest(List<string?>? Respuestas);

    [HttpPut("{id:guid}/requirements/{code}/clarifications")]
    public Task<IActionResult> AnswerClarifications(Guid id, string code,
        [FromBody] ClarificationAnswersRequest? request, CancellationToken cancellationToken)
        => ExecuteAsync(() => _orchestrator.AnswerClarificationsAsync(
            id, code, request?.Respuestas ?? new List<string?>(), cancellationToken));

    [HttpPost("{id:guid}/requirements/{code}/stories")]
    public Task<IActionResult> GenerateStories(Guid id, string code, [FromQuery] bool regenerar,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _orchestrator.GenerateStoriesAsync(id, code, regenerar, cancellationToken));

    /// <summary>Genera el backlog de historias de usuario (ADR-0006) para un requerimiento listo.</summary>
    [HttpPost("{id:guid}/requirements/{code}/user-stories")]
    public Task<IActionResult> GenerateUserStories(Guid id, string code, [FromQuery] bool regenerar,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _orchestrator.GenerateUserStoriesAsync(id, code, regenerar, cancellationToken));

    /// <summary>Reemplaza el caso de uso de un requerimiento con una edición manual.</summary>
    [HttpPut("{id:guid}/requirements/{code}/use-case")]
    public Task<IActionResult> EditUseCase(Guid id, string code, [FromBody] UseCaseEditDto? request,
        CancellationToken cancellationToken)
        => request is null
            ? Task.FromResult<IActionResult>(new BadRequestObjectResult(new { mensaje = "Debe enviar el caso de uso." }))
            : ExecuteAsync(() => _orchestrator.UpdateUseCaseAsync(id, code, request, cancellationToken));

    public sealed record StageRequest(string? Etapa);

    /// <summary>Cambia la etapa de gestión del requerimiento (Draft/Approved/InDevelopment/Implemented).</summary>
    [HttpPut("{id:guid}/requirements/{code}/stage")]
    public Task<IActionResult> SetStage(Guid id, string code, [FromBody] StageRequest? request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => _orchestrator.SetStageAsync(id, code, request?.Etapa ?? string.Empty, cancellationToken));

    [HttpPost("{id:guid}/requirements/{code}/reevaluate")]
    public Task<IActionResult> Reevaluate(Guid id, string code,
        [FromBody] ClarificationAnswersRequest? request, CancellationToken cancellationToken)
        => ExecuteAsync(() => _orchestrator.ReevaluateAsync(
            id, code, request?.Respuestas ?? new List<string?>(), cancellationToken));

    private static async Task<IActionResult> ExecuteAsync(Func<Task<RequirementDetailDto>> action)
    {
        try
        {
            return new OkObjectResult(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(new { mensaje = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return new BadRequestObjectResult(new { mensaje = ex.Message });
        }
        catch (LlmException ex)
        {
            return new ObjectResult(new { mensaje = ex.Message }) { StatusCode = StatusCodes.Status502BadGateway };
        }
        catch (InvalidOperationException ex)
        {
            return new ConflictObjectResult(new { mensaje = ex.Message });
        }
    }

    private async Task WriteEventAsync(AnalysisEvent analysisEvent, CancellationToken cancellationToken)
    {
        string name = analysisEvent.Kind.ToString().ToLowerInvariant();
        object payload = analysisEvent.Kind switch
        {
            AnalysisEventKind.Status => new { mensaje = analysisEvent.Message },
            AnalysisEventKind.Requirement => new { requerimiento = analysisEvent.Requirement },
            AnalysisEventKind.Evaluation => new { evaluacion = analysisEvent.Evaluation },
            AnalysisEventKind.Clarification => new { aclaracion = analysisEvent.Clarification },
            AnalysisEventKind.Overlap => new { solapamiento = analysisEvent.Overlap },
            AnalysisEventKind.Summary => new { resumen = analysisEvent.Summary },
            AnalysisEventKind.Done => new { analysisId = analysisEvent.AnalysisId },
            AnalysisEventKind.Error => new { mensaje = analysisEvent.Message },
            _ => new { },
        };
        string data = JsonSerializer.Serialize(payload, JsonOptions);
        await Response.WriteAsync($"event: {name}\ndata: {data}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
