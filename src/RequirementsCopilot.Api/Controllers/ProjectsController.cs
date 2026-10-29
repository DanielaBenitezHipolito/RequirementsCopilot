using Microsoft.AspNetCore.Mvc;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Projects;

namespace RequirementsCopilot.Api.Controllers;

/// <summary>
/// Proyectos existentes: cada uno es un archivo Markdown con la descripción del sistema. Al analizar
/// o conversar se elige uno para que los agentes no pregunten por funcionamiento ya documentado.
/// </summary>
[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private const long MaxFileBytes = 2 * 1024 * 1024;

    public sealed record UpdateRequest(string? Contenido);

    private static readonly string[] DocumentExtensions = { ".pdf", ".docx", ".txt", ".md" };

    private readonly IProjectRepository _projects;
    private readonly IAnalysisRepository _analyses;
    private readonly IDocumentTextExtractor _textExtractor;

    public ProjectsController(IProjectRepository projects, IAnalysisRepository analyses,
        IDocumentTextExtractor textExtractor)
        => (_projects, _analyses, _textExtractor) = (projects, analyses, textExtractor);

    /// <summary>Lista los proyectos disponibles (nombre y fecha de actualización).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<Project> projects = await _projects.GetAllAsync(cancellationToken);
        return Ok(projects.Select(p => new { nombre = p.Name, updatedAt = p.UpdatedAt }));
    }

    /// <summary>Devuelve el contenido Markdown de un proyecto y cuántos casos de uso generados tiene asociados.</summary>
    [HttpGet("{nombre}")]
    public async Task<IActionResult> GetByName(string nombre, CancellationToken cancellationToken)
    {
        Project? project = await _projects.GetByNameAsync(nombre, cancellationToken);
        if (project is null)
        {
            return NotFound(new { mensaje = "Proyecto no encontrado." });
        }
        int casosDeUso = (await _analyses.GetByProjectAsync(project.Name, cancellationToken))
            .SelectMany(a => a.Requirements).Count(r => r.UseCase is not null);
        return Ok(new { nombre = project.Name, contenido = project.Content, updatedAt = project.UpdatedAt, casosDeUso });
    }

    /// <summary>
    /// Sube (o reemplaza) el .md de un proyecto. El nombre es el campo <c>nombre</c> o, si falta,
    /// el nombre del archivo sin extensión. Sin archivo y con <c>nombre</c>, crea un proyecto vacío
    /// (ADR-0009): su contexto se construye con los casos de uso que genere.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxFileBytes + 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, [FromForm] string? nombre, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return BadRequest(new { mensaje = "Debe adjuntar un archivo .md o indicar un nombre de proyecto." });
            }
            Project emptyProject = Project.Create(nombre, string.Empty);
            await _projects.SaveAsync(emptyProject, cancellationToken);
            return Ok(new { nombre = emptyProject.Name, updatedAt = emptyProject.UpdatedAt });
        }
        if (file.Length > MaxFileBytes)
        {
            return BadRequest(new { mensaje = "El archivo supera el máximo de 2 MB." });
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".md", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { mensaje = "Solo se admiten archivos Markdown (.md)." });
        }

        string name = string.IsNullOrWhiteSpace(nombre) ? Path.GetFileNameWithoutExtension(file.FileName) : nombre;
        using StreamReader reader = new(file.OpenReadStream());
        string content = await reader.ReadToEndAsync(cancellationToken);

        Project project;
        try
        {
            project = Project.Create(name, content);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }

        await _projects.SaveAsync(project, cancellationToken);
        return Ok(new { nombre = project.Name, updatedAt = project.UpdatedAt });
    }

    /// <summary>Todos los casos de uso generados del proyecto, para la exportación consolidada (ADR-0009).</summary>
    [HttpGet("{nombre}/use-cases")]
    public async Task<IActionResult> GetUseCases(string nombre, CancellationToken cancellationToken)
    {
        Project? project = await _projects.GetByNameAsync(nombre, cancellationToken);
        if (project is null)
        {
            return NotFound(new { mensaje = "Proyecto no encontrado." });
        }
        RequirementDetailDto[] casos = (await _analyses.GetByProjectAsync(project.Name, cancellationToken))
            .SelectMany(a => a.Requirements)
            .Where(r => r.UseCase is not null)
            .Select(AnalysisQueries.MapRequirement)
            .ToArray();
        return Ok(casos);
    }

    /// <summary>
    /// Anexa al contenido del proyecto el texto extraído de un documento (casos de uso existentes
    /// en Word/PDF/texto): queda como parte del contexto que reciben los agentes (ADR-0009).
    /// </summary>
    [HttpPost("{nombre}/documents")]
    [RequestSizeLimit(10 * 1024 * 1024 + 1024)]
    public async Task<IActionResult> AppendDocument(string nombre, IFormFile? file, CancellationToken cancellationToken)
    {
        Project? project = await _projects.GetByNameAsync(nombre, cancellationToken);
        if (project is null)
        {
            return NotFound(new { mensaje = "Proyecto no encontrado." });
        }
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { mensaje = "Debe adjuntar un documento." });
        }
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!DocumentExtensions.Contains(extension))
        {
            return BadRequest(new { mensaje = $"Formato no soportado: {extension}. Use PDF, DOCX, TXT o MD." });
        }

        await using Stream content = file.OpenReadStream();
        string texto;
        try
        {
            texto = (await _textExtractor.ExtractAsync(content, file.FileName, cancellationToken)).Text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BadRequest(new { mensaje = $"No se pudo leer el documento: {ex.Message}" });
        }
        if (string.IsNullOrWhiteSpace(texto))
        {
            return BadRequest(new { mensaje = "El documento no contiene texto legible para anexar al proyecto." });
        }

        string updated = string.IsNullOrWhiteSpace(project.Content)
            ? $"## Importado de «{file.FileName}»\n\n{texto.Trim()}"
            : $"{project.Content}\n\n## Importado de «{file.FileName}»\n\n{texto.Trim()}";
        Project saved = Project.Create(project.Name, updated);
        await _projects.SaveAsync(saved, cancellationToken);
        return Ok(new { nombre = saved.Name, updatedAt = saved.UpdatedAt, caracteres = saved.Content.Length });
    }

    /// <summary>Reemplaza el contenido Markdown de un proyecto existente (edición en pantalla).</summary>
    [HttpPut("{nombre}")]
    public async Task<IActionResult> Update(string nombre, [FromBody] UpdateRequest? request, CancellationToken cancellationToken)
    {
        Project? existing = await _projects.GetByNameAsync(nombre, cancellationToken);
        if (existing is null)
        {
            return NotFound(new { mensaje = "Proyecto no encontrado." });
        }
        Project project;
        try
        {
            project = Project.Create(existing.Name, request?.Contenido ?? string.Empty);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        await _projects.SaveAsync(project, cancellationToken);
        return Ok(new { nombre = project.Name, updatedAt = project.UpdatedAt });
    }

    /// <summary>Elimina un proyecto. Los análisis ya hechos conservan el nombre, pero dejan de recibir contexto.</summary>
    [HttpDelete("{nombre}")]
    public async Task<IActionResult> Delete(string nombre, CancellationToken cancellationToken)
        => await _projects.DeleteAsync(nombre, cancellationToken)
            ? NoContent()
            : NotFound(new { mensaje = "Proyecto no encontrado." });
}
