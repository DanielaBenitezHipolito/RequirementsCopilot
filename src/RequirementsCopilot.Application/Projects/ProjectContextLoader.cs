using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Application.Projects;

/// <summary>
/// Resuelve el bloque de contexto que se antepone al input de los agentes cuando el requerimiento
/// pertenece a un proyecto existente. Sin proyecto (nuevo) devuelve cadena vacía.
/// </summary>
public sealed class ProjectContextLoader
{
    private readonly IProjectRepository _projects;
    private readonly AnalysisOptions _options;

    /// <summary>Crea el cargador sobre el repositorio de proyectos.</summary>
    public ProjectContextLoader(IProjectRepository projects, AnalysisOptions options)
        => (_projects, _options) = (projects, options);

    /// <summary>Bloque de contexto para el proyecto indicado, o vacío si no se seleccionó / no existe.</summary>
    public async Task<string> LoadAsync(string? projectName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return string.Empty;
        }
        Project? project = await _projects.GetByNameAsync(projectName, cancellationToken);
        return project is null ? string.Empty : Format(project, _options.MaxProjectContextChars);
    }

    /// <summary>Formatea el contexto con la instrucción de no preguntar por lo ya documentado.</summary>
    public static string Format(Project project, int maxChars)
    {
        // Control de costos: el .md se trunca antes de ir al LLM.
        string content = project.Content.Length > maxChars ? project.Content[..maxChars] : project.Content;
        return $"CONTEXTO DEL PROYECTO EXISTENTE «{project.Name}»\n" +
               "El sistema YA existe y funciona como se describe a continuación. NO hagas preguntas sobre " +
               "funcionalidades, módulos, roles, integraciones o reglas ya descritas aquí: asúmelas como dadas. " +
               "Pregunta y evalúa SOLO lo nuevo que el requerimiento introduce o cambia.\n\n" +
               $"{content}\n\n=== FIN DEL CONTEXTO DEL PROYECTO ===\n\n";
    }
}
