using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class ExecutiveSummaryAgent
{
    public const string AgentName = "executive-summary-agent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public ExecutiveSummaryAgent(IChatCompletion chat) => _chat = chat;

    public async Task<string> SummarizeAsync(Analysis analysis, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            archivo = analysis.FileName,
            umbral = analysis.Requirements.FirstOrDefault(r => r.Evaluation is not null)?.Evaluation!.Threshold ?? 0,
            requerimientos = analysis.Requirements.Select(r => new
            {
                codigo = r.Code,
                area = r.Area,
                promedio = r.Evaluation is null ? 0 : Math.Round(r.Evaluation.Average, 2),
                pasa = r.Evaluation?.Passed ?? false,
                observacionesClave = (r.Evaluation?.Scores ?? Array.Empty<CriterionScore>())
                    .Where(s => s.Score <= 3)
                    .Take(2)
                    .Select(s => s.Observation)
                    .ToArray(),
            }).ToArray(),
        };
        string input = JsonSerializer.Serialize(payload, JsonOptions);

        string json = await _chat.CompleteJsonAsync(new ChatPrompt(AgentName, input), cancellationToken)
            ?? throw new InvalidOperationException("El agente de resumen ejecutivo no devolvió JSON válido.");
        SummaryReply reply = JsonSerializer.Deserialize<SummaryReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente de resumen ejecutivo devolvió una respuesta vacía.");

        if (string.IsNullOrWhiteSpace(reply.Resumen))
        {
            throw new InvalidOperationException("El agente de resumen ejecutivo no devolvió un resumen válido.");
        }

        return reply.Resumen.Trim();
    }

    private sealed record SummaryReply(string? Resumen);
}
