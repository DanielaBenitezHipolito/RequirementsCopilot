namespace RequirementsCopilot.Application.Projects;

/// <summary>
/// Proyecto/sistema existente descrito en un archivo Markdown. Su contenido se inyecta como contexto
/// a los agentes para que no pregunten por funcionamiento que ya está documentado.
/// </summary>
public sealed record Project(string Name, string Content, DateTime UpdatedAt)
{
    /// <summary>Documentos de ejemplo (solo lectura): guía de formato/tono para los agentes.</summary>
    public IReadOnlyList<ProjectExample> Examples { get; init; } = Array.Empty<ProjectExample>();

    /// <summary>Agrega (o reemplaza por nombre) un documento de ejemplo.</summary>
    public Project AddExample(string name, string content)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("El documento de ejemplo requiere nombre y contenido.", nameof(name));
        }
        ProjectExample[] examples = Examples
            .Where(e => !string.Equals(e.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
            .Append(new ProjectExample(name.Trim(), content.Trim()))
            .ToArray();
        return this with { Examples = examples, UpdatedAt = DateTime.UtcNow };
    }

    /// <summary>Elimina un documento de ejemplo por nombre; null si no existe.</summary>
    public Project? RemoveExample(string name)
    {
        ProjectExample[] examples = Examples
            .Where(e => !string.Equals(e.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return examples.Length == Examples.Count
            ? null
            : this with { Examples = examples, UpdatedAt = DateTime.UtcNow };
    }

    /// <summary>
    /// Crea un proyecto validando el nombre. El contenido puede estar vacío: un proyecto nuevo
    /// arranca sin descripción y su contexto se construye con los casos de uso que genere (ADR-0009).
    /// </summary>
    public static Project Create(string name, string? content)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El proyecto requiere nombre.", nameof(name));
        }
        return new Project(name.Trim(), content?.Trim() ?? string.Empty, DateTime.UtcNow);
    }
}
