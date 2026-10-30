namespace RequirementsCopilot.Application.Analyses.Agents;

/// <summary>Pregunta de clarificación generada por el agente, con opciones sugeridas si las hay.</summary>
/// <param name="Text">Texto de la pregunta.</param>
/// <param name="Options">Opciones de respuesta sugeridas (vacío si es abierta).</param>
public sealed record ClarifierQuestion(string Text, IReadOnlyList<string> Options);
