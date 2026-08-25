namespace RequirementsCopilot.Application.Projects;

/// <summary>
/// Proyecto/sistema existente descrito en un archivo Markdown. Su contenido se inyecta como contexto
/// a los agentes para que no pregunten por funcionamiento que ya está documentado.
/// </summary>
public sealed record Project(string Name, string Content, DateTime UpdatedAt)
{
    /// <summary>Crea un proyecto validando nombre y contenido.</summary>
    public static Project Create(string name, string content)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El proyecto requiere nombre.", nameof(name));
        }
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("El archivo del proyecto está vacío.", nameof(content));
        }
        return new Project(name.Trim(), content.Trim(), DateTime.UtcNow);
    }
}
