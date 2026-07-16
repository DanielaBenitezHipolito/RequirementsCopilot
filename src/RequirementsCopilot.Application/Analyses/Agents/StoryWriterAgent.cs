using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class StoryWriterAgent
{
    public const string AgentName = "story-writer-agent";

    private const string Instructions =
        "Eres un product owner. A partir del requerimiento aprobado, escribe las historias de usuario necesarias " +
        "(mínimo 1, máximo 4), cada una con rol, objetivo (quiero), beneficio (para) y de 1 a 4 criterios de aceptación " +
        "verificables en formato dado/cuando/entonces. " +
        "Responde ÚNICAMENTE este JSON: {\"historias\":[{\"rol\":\"...\",\"quiero\":\"...\",\"para\":\"...\",\"criteriosAceptacion\":[\"...\"]}]} " +
        "No inventes funcionalidad que el requerimiento no mencione.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public StoryWriterAgent(IChatCompletion chat) => _chat = chat;

    public async Task<IReadOnlyList<UserStory>> WriteAsync(Requirement requirement, CancellationToken cancellationToken = default)
    {
        var answered = requirement.Clarifications.Where(c => c.IsAnswered).ToArray();
        string clarifications = answered.Length == 0
            ? string.Empty
            : "\n\nAclaraciones del cliente (úsalas para no malinterpretar):\n" +
              string.Join("\n", answered.Select(c => $"- P: {c.Question} R: {c.Answer}"));
        ChatResult result = await _chat.CompleteAsync(
            new ChatPrompt(AgentName, Instructions,
                $"Requerimiento {requirement.Code} (área {requirement.Area}):\n{requirement.Text}{clarifications}"),
            cancellationToken);

        string json = JsonText.FirstJsonObject(result.Text)
            ?? throw new InvalidOperationException("El agente de historias no devolvió JSON válido.");
        StoriesReply reply = JsonSerializer.Deserialize<StoriesReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente de historias devolvió una respuesta vacía.");

        return (reply.Historias ?? new List<StoryItem>())
            .Where(h => !string.IsNullOrWhiteSpace(h.Quiero))
            .Select(h => UserStory.Create(h.Rol ?? string.Empty, h.Quiero!, h.Para ?? string.Empty,
                h.CriteriosAceptacion ?? new List<string>()))
            .ToArray();
    }

    private sealed record StoriesReply(List<StoryItem>? Historias);

    private sealed record StoryItem(string? Rol, string? Quiero, string? Para, List<string>? CriteriosAceptacion);
}
