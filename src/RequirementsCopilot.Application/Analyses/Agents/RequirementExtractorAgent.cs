using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class RequirementExtractorAgent
{
    public const string AgentName = "requirement-extractor-agent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public RequirementExtractorAgent(IChatCompletion chat) => _chat = chat;

    public async Task<IReadOnlyList<Requirement>> ExtractAsync(string documentText, CancellationToken cancellationToken = default)
    {
        ChatResult result = await _chat.CompleteAsync(
            new ChatPrompt(AgentName, $"Documento:\n{documentText}"), cancellationToken);

        string json = JsonText.FirstJsonObject(result.Text)
            ?? throw new InvalidOperationException("El agente extractor no devolvió JSON válido.");
        ExtractorReply reply = JsonSerializer.Deserialize<ExtractorReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente extractor devolvió una respuesta vacía.");

        return (reply.Requerimientos ?? new List<ExtractedRequirement>())
            .Where(r => !string.IsNullOrWhiteSpace(r.Codigo) && !string.IsNullOrWhiteSpace(r.Texto))
            .Select(r => Requirement.Create(r.Codigo!, r.Texto!, r.Area ?? "General"))
            .ToArray();
    }

    private sealed record ExtractorReply(List<ExtractedRequirement>? Requerimientos);

    private sealed record ExtractedRequirement(string? Codigo, string? Texto, string? Area);
}
