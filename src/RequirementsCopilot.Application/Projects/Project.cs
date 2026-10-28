namespace RequirementsCopilot.Application.Projects;

/// <summary>
/// Proyecto/sistema existente descrito en un archivo Markdown. Su contenido se inyecta como contexto
/// a los agentes para que no pregunten por funcionamiento que ya está documentado.
/// </summary>
public sealed record Project(string Name, string Content, DateTime UpdatedAt)
{
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
