namespace RequirementsCopilot.Application.Analyses;

/// <summary>Respuesta de un agente. <paramref name="ResponseId"/> permite continuar el hilo en el siguiente turno.</summary>
public sealed record ChatResult(string Text, string? ResponseId = null);
