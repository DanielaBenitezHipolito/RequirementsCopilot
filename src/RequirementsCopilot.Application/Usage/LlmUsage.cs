namespace RequirementsCopilot.Application.Usage;

/// <summary>Consumo de una llamada al LLM, para el tablero de control de costos (ADR-0010).</summary>
/// <param name="Timestamp">Momento UTC de la llamada.</param>
/// <param name="Agent">Código lógico del agente que llamó.</param>
/// <param name="Model">Modelo/deployment usado.</param>
/// <param name="InputTokens">Tokens de entrada reportados por la API.</param>
/// <param name="OutputTokens">Tokens de salida reportados por la API.</param>
public sealed record LlmUsage(DateTime Timestamp, string Agent, string Model, int InputTokens, int OutputTokens);
