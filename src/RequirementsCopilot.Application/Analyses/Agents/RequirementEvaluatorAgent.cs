using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class RequirementEvaluatorAgent
{
    public const string AgentName = "requirement-evaluator-agent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public RequirementEvaluatorAgent(IChatCompletion chat) => _chat = chat;

    public async Task<Evaluation> EvaluateAsync(Requirement requirement, double threshold, CancellationToken cancellationToken = default)
    {
        ChatResult result = await _chat.CompleteAsync(
            new ChatPrompt(AgentName, $"Requerimiento {requirement.Code} (área {requirement.Area}):\n{requirement.Text}"),
            cancellationToken);

        string json = JsonText.FirstJsonObject(result.Text)
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
