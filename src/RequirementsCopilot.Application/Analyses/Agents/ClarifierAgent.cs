using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class ClarifierAgent
{
    public const string AgentName = "clarifier-agent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public ClarifierAgent(IChatCompletion chat) => _chat = chat;

    /// <summary>Genera preguntas; <paramref name="projectContext"/> evita preguntar por lo ya documentado.</summary>
    public async Task<IReadOnlyList<string>> AskAsync(Requirement requirement, string? projectContext = null,
        CancellationToken cancellationToken = default)
    {
        string observations = requirement.Evaluation is null
            ? string.Empty
            : "\nObservaciones de la rúbrica:\n" + string.Join("\n",
                requirement.Evaluation.Scores.Select(s => $"- {s.Criterion} ({s.Score}/5): {s.Observation}"));

        // Preguntas ya respondidas en rondas previas: se pasan para que NO se repitan.
        var answered = requirement.Clarifications.Where(c => c.IsAnswered).ToArray();
        string answeredBlock = answered.Length == 0
            ? string.Empty
            : "\n\nPreguntas YA respondidas (NO las repitas ni pidas de nuevo lo que su respuesta resuelve; " +
              "pregunta SOLO por ambigüedades que sigan sin resolver):\n" +
              string.Join("\n", answered.Select(c => $"- P: {c.Question}\n  R: {c.Answer}"));

        string json = await _chat.CompleteJsonAsync(
            new ChatPrompt(AgentName,
                $"{projectContext}Requerimiento {requirement.Code} (área {requirement.Area}):\n{requirement.Text}{observations}{answeredBlock}"),
            cancellationToken)
            ?? throw new InvalidOperationException("El agente clarificador no devolvió JSON válido.");
        ClarifierReply reply = JsonSerializer.Deserialize<ClarifierReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente clarificador devolvió una respuesta vacía.");

        return (reply.Preguntas ?? new List<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToArray();
    }

    private sealed record ClarifierReply(List<string>? Preguntas);
}
