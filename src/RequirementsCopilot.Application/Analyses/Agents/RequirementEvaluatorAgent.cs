using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class RequirementEvaluatorAgent
{
    public const string AgentName = "requirement-evaluator-agent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public RequirementEvaluatorAgent(IChatCompletion chat) => _chat = chat;

    public Task<Evaluation> EvaluateAsync(Requirement requirement, double threshold, CancellationToken cancellationToken = default)
        => EvaluateAsync(requirement, threshold, answeredClarifications: null, projectContext: null, cancellationToken);

    /// <summary>Re-evaluación tras responder aclaraciones: el input incluye las preguntas ya respondidas.</summary>
    public async Task<Evaluation> EvaluateAsync(Requirement requirement, double threshold,
        IReadOnlyList<Clarification>? answeredClarifications, string? projectContext = null,
        CancellationToken cancellationToken = default)
    {
        var answered = (answeredClarifications ?? Array.Empty<Clarification>()).Where(c => c.IsAnswered).ToArray();
        string clarifications = answered.Length == 0
            ? string.Empty
            : "\n\nAclaraciones respondidas (el requerimiento ya incorpora estas respuestas; deben MANTENER o " +
              "SUBIR los puntajes, nunca bajarlos):\n" +
              string.Join("\n", answered.Select(c => $"- P: {c.Question}\n  R: {c.Answer}"));

        // Ancla: en la re-evaluación se envía el puntaje previo para que no retroceda por variabilidad del modelo.
        string previous = answered.Length == 0 || requirement.Evaluation is null
            ? string.Empty
            : "\n\nPuntaje previo (antes de estas aclaraciones) — no debe bajar:\n" +
              string.Join("\n", requirement.Evaluation.Scores.Select(s => $"- {s.Criterion}: {s.Score}/5"));

        string json = await _chat.CompleteJsonAsync(
            new ChatPrompt(AgentName,
                $"{projectContext}Requerimiento {requirement.Code} (área {requirement.Area}):\n{requirement.Text}{clarifications}{previous}"),
            cancellationToken)
            ?? throw new InvalidOperationException("El agente evaluador no devolvió JSON válido.");
        EvaluatorReply reply = JsonSerializer.Deserialize<EvaluatorReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente evaluador devolvió una respuesta vacía.");

        CriterionScore[] scores = (reply.Criterios ?? new List<EvaluatedCriterion>())
            .Where(c => !string.IsNullOrWhiteSpace(c.Nombre) && c.Score is >= 1 and <= 5)
            .Select(c => CriterionScore.Create(c.Nombre!, c.Score, c.Observacion ?? string.Empty))
            .ToArray();
        if (scores.Length == 0)
        {
            throw new InvalidOperationException("El agente evaluador no devolvió criterios válidos.");
        }

        return Evaluation.Create(scores, threshold);
    }

    private sealed record EvaluatorReply(List<EvaluatedCriterion>? Criterios);

    private sealed record EvaluatedCriterion(string? Nombre, int Score, string? Observacion);
}
