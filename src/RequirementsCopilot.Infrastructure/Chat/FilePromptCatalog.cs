namespace RequirementsCopilot.Infrastructure.Chat;

/// <summary>
/// Catálogo de prompts de sistema por agente, cargado una vez desde la carpeta <c>prompts/</c>
/// del despliegue (un <c>.md</c> por agente, nombre de archivo = código lógico del agente).
/// Los prompts se versionan en el repo y viajan en cada request (<c>instructions</c>): cambiar
/// un prompt en producción es commit + deploy, sin tocar el portal Foundry (ADR-0008).
/// </summary>
public sealed class FilePromptCatalog
{
    private readonly IReadOnlyDictionary<string, string> _prompts;

    /// <summary>Carga todos los <c>.md</c> de <paramref name="directory"/>; falla al arrancar si la carpeta no existe.</summary>
    /// <param name="directory">Carpeta con un archivo de prompt por agente.</param>
    public FilePromptCatalog(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"No existe la carpeta de prompts: {directory}. Debe desplegarse junto a la aplicación.");
        }
        _prompts = Directory.GetFiles(directory, "*.md")
            .ToDictionary(file => Path.GetFileNameWithoutExtension(file), File.ReadAllText);
    }

    /// <summary>Prompt de sistema del agente <paramref name="agent"/>; lanza si no hay archivo para él.</summary>
    /// <param name="agent">Código lógico del agente (p. ej. <c>requirement-extractor-agent</c>).</param>
    public string Get(string agent)
        => _prompts.TryGetValue(agent, out string? prompt)
            ? prompt
            : throw new KeyNotFoundException($"No hay prompt para el agente «{agent}» (falta prompts/{agent}.md).");
}
