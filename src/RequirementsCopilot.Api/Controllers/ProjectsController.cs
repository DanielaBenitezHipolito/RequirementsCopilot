using Microsoft.AspNetCore.Mvc;
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

    private readonly IProjectRepository _projects;

    public ProjectsController(IProjectRepository projects) => _projects = projects;

    /// <summary>Lista los proyectos disponibles (nombre y fecha de actualización).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<Project> projects = await _projects.GetAllAsync(cancellationToken);
        return Ok(projects.Select(p => new { nombre = p.Name, updatedAt = p.UpdatedAt }));
    }

    /// <summary>Devuelve el contenido Markdown de un proyecto.</summary>
    [HttpGet("{nombre}")]
    public async Task<IActionResult> GetByName(string nombre, CancellationToken cancellationToken)
    {
        Project? project = await _projects.GetByNameAsync(nombre, cancellationToken);
        return project is null
            ? NotFound(new { mensaje = "Proyecto no encontrado." })
            : Ok(new { nombre = project.Name, contenido = project.Content, updatedAt = project.UpdatedAt });
    }

    /// <summary>
    /// Sube (o reemplaza) el .md de un proyecto. El nombre es el campo <c>nombre</c> o, si falta,
    /// el nombre del archivo sin extensión.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxFileBytes + 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, [FromForm] string? nombre, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { mensaje = "Debe adjuntar un archivo .md." });
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
