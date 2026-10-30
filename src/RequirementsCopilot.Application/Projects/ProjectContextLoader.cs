using System.Text;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Projects;

/// <summary>
/// Resuelve el bloque de contexto que se antepone al input de los agentes cuando el requerimiento
/// pertenece a un proyecto: la descripción Markdown del proyecto más los casos de uso ya definidos
/// para él (ADR-0009), para que lo nuevo no duplique ni contradiga lo existente.
/// Sin proyecto (nuevo) devuelve cadena vacía.
/// </summary>
public sealed class ProjectContextLoader
{
    private readonly IProjectRepository _projects;
    private readonly IAnalysisRepository _analyses;
    private readonly AnalysisOptions _options;

    /// <summary>Crea el cargador sobre los repositorios de proyectos y de análisis.</summary>
    public ProjectContextLoader(IProjectRepository projects, IAnalysisRepository analyses, AnalysisOptions options)
        => (_projects, _analyses, _options) = (projects, analyses, options);

    /// <summary>Bloque de contexto para el proyecto indicado, o vacío si no se seleccionó.</summary>
    public async Task<string> LoadAsync(string? projectName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return string.Empty;
        }
        Project? project = await _projects.GetByNameAsync(projectName, cancellationToken);
        // Proyecto nuevo sin .md: el bloque descriptivo se omite y el contexto son solo sus casos de uso.
        string description = project is null || string.IsNullOrWhiteSpace(project.Content)
            ? string.Empty
            : Format(project, _options.MaxProjectContextChars);
        string examples = project is null
            ? string.Empty
            : FormatExamples(project, _options.MaxProjectContextChars);
        string useCases = FormatUseCases(project?.Name ?? projectName.Trim(),
            await _analyses.GetByProjectAsync(projectName, cancellationToken), _options.MaxProjectContextChars);
        return description + examples + useCases;
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

    /// <summary>
    /// Documentos de ejemplo del proyecto (plantillas, entregables modelo): material de SOLO lectura
    /// que guía formato y tono, nunca fuente de requerimientos; vacío si el proyecto no tiene.
    /// </summary>
    private static string FormatExamples(Project project, int maxChars)
    {
        if (project.Examples.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        builder.Append($"DOCUMENTOS DE EJEMPLO DEL PROYECTO «{project.Name}»\n")
            .Append("Material de referencia de solo lectura (plantillas y entregables modelo): úsalo como guía ")
            .Append("de formato, tono y nivel de detalle de lo que se espera producir. NO extraigas requerimientos ")
            .Append("de aquí ni lo trates como funcionalidad existente del sistema.\n");
        foreach (ProjectExample example in project.Examples)
        {
            string entry = $"\n--- Ejemplo «{example.Name}» ---\n{example.Content}\n";
            if (builder.Length + entry.Length > maxChars)
            {
                // Control de costos: con ejemplos muy largos se conservan los primeros completos.
                break;
            }
            builder.Append(entry);
        }
        builder.Append("\n=== FIN DE DOCUMENTOS DE EJEMPLO ===\n\n");
        return builder.ToString();
    }

    /// <summary>
    /// Resumen compacto (nombre, objetivo, actores) de los casos de uso ya generados del proyecto,
    /// con la instrucción de no duplicarlos; vacío si el proyecto aún no tiene casos de uso.
    /// </summary>
    private static string FormatUseCases(string projectName, IReadOnlyList<Analysis> analyses, int maxChars)
    {
        UseCase[] useCases = analyses
            .SelectMany(a => a.Requirements)
            .Where(r => r.UseCase is not null)
            .Select(r => r.UseCase!)
            .ToArray();
        if (useCases.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        builder.Append($"CASOS DE USO YA DEFINIDOS DEL PROYECTO «{projectName}»\n")
            .Append("Estos casos de uso ya fueron redactados para este proyecto. NO los dupliques ni los ")
            .Append("redefinas: lo nuevo debe ser complementario y mantener actores y terminología ")
            .Append("consistentes con los existentes.\n\n");
        int index = 0;
        foreach (UseCase useCase in useCases)
        {
            string actores = string.Join(", ", useCase.Actores.Select(a => a.Nombre));
            string entry = $"{++index}. «{useCase.Nombre}» — {useCase.Objetivo}." +
                           (actores.Length > 0 ? $" Actores: {actores}." : string.Empty) + "\n";
            if (builder.Length + entry.Length > maxChars)
            {
                // Control de costos: con muchos casos de uso se conservan los primeros (orden cronológico).
                break;
            }
            builder.Append(entry);
        }
        builder.Append("\n=== FIN DE CASOS DE USO YA DEFINIDOS ===\n\n");
        return builder.ToString();
    }
}
