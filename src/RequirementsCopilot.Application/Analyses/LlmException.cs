namespace RequirementsCopilot.Application.Analyses;

/// <summary>Fallo del proveedor LLM (agente evaluador/clarificador) durante un flujo síncrono. Mapea a 502 en la Api.</summary>
public sealed class LlmException : Exception
{
    public LlmException(string message) : base(message) { }
}
