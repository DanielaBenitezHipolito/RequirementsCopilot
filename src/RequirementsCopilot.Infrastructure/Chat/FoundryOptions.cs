namespace RequirementsCopilot.Infrastructure.Chat;

/// <summary>Opciones de Azure AI Foundry (patrón JYDE: agentes publicados, Responses API).</summary>
public sealed class FoundryOptions
{
    /// <summary>Endpoint del proyecto (p. ej. https://recurso.services.ai.azure.com/api/projects/proyecto).</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modelo por defecto y catálogo de agentes publicados.</summary>
    public FoundryChatSettings Chat { get; set; } = new();
}

public sealed class FoundryChatSettings
{
    public string Model { get; set; } = "gpt-5-mini";

    /// <summary>Tope de tokens de salida por llamada (control de costos); null = sin tope.</summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>Código lógico del agente (AgentName en código) → agente publicado en Foundry.</summary>
    public Dictionary<string, FoundryAgentSettings> Agents { get; set; } = new();
}

public sealed class FoundryAgentSettings
{
    /// <summary>Nombre del agente tal como está publicado en Foundry.</summary>
    public string? Name { get; set; }

    public string? Version { get; set; }

    /// <summary>Override del modelo por defecto.</summary>
    public string? Model { get; set; }
}
