namespace RequirementsCopilot.Infrastructure.Chat;

/// <summary>Opciones de Azure AI Foundry (Responses API; prompts versionados en el repo, ADR-0008).</summary>
public sealed class FoundryOptions
{
    /// <summary>Endpoint del proyecto (p. ej. https://recurso.services.ai.azure.com/api/projects/proyecto).</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modelo por defecto y overrides por agente.</summary>
    public FoundryChatSettings Chat { get; set; } = new();
}

public sealed class FoundryChatSettings
{
    public string Model { get; set; } = "gpt-5-mini";

    /// <summary>Tope de tokens de salida por llamada (control de costos); null = sin tope.</summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>Código lógico del agente (AgentName en código) → overrides opcionales.</summary>
    public Dictionary<string, FoundryAgentSettings> Agents { get; set; } = new();
}

public sealed class FoundryAgentSettings
{
    /// <summary>Override del modelo por defecto para este agente.</summary>
    public string? Model { get; set; }
}
