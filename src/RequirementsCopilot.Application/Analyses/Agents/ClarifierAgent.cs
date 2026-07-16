using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class ClarifierAgent
{
    public const string AgentName = "clarifier-agent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public ClarifierAgent(IChatCompletion chat) => _chat = chat;

    public async Task<IReadOnlyList<string>> AskAsync(Requirement requirement, CancellationToken cancellationToken = default)
    {
        string observations = requirement.Evaluation is null
            ? string.Empty
            : "\nObservaciones de la rúbrica:\n" + string.Join("\n",
                requirement.Evaluation.Scores.Select(s => $"- {s.Criterion} ({s.Score}/5): {s.Observation}"));
        ChatResult result = await _chat.CompleteAsync(
            new ChatPrompt(AgentName,
                $"Requerimiento {requirement.Code} (área {requirement.Area}):\n{requirement.Text}{observations}"),
            cancellationToken);

        string json = JsonText.FirstJsonObject(result.Text)
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
