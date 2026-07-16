namespace RequirementsCopilot.Application.Analyses;

/// <summary>Prompt para un agente. <paramref name="PreviousResponseId"/> mantiene el hilo conversacional (Responses API).</summary>
public sealed record ChatPrompt(string Agent, string Input, string? PreviousResponseId = null);
