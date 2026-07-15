using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class TestCaseWriterAgent
{
    public const string AgentName = "test-case-writer-agent";

    private const string Instructions =
        "Eres un ingeniero de QA. A partir de la historia de usuario dada, escribe UN caso de prueba funcional que valide " +
        "sus criterios de aceptación: título corto, precondiciones, pasos numerables concretos y resultado esperado verificable. " +
        "Responde ÚNICAMENTE este JSON: {\"titulo\":\"...\",\"precondiciones\":[\"...\"],\"pasos\":[\"...\"],\"resultadoEsperado\":\"...\"}";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public TestCaseWriterAgent(IChatCompletion chat) => _chat = chat;

    public async Task<TestCase> WriteAsync(UserStory story, CancellationToken cancellationToken = default)
    {
        string input = $"Historia: como {story.Role}, quiero {story.Goal}, para {story.Benefit}.\n" +
            $"Criterios de aceptación:\n- {string.Join("\n- ", story.AcceptanceCriteria)}";
        ChatResult result = await _chat.CompleteAsync(new ChatPrompt(AgentName, Instructions, input), cancellationToken);

        string json = JsonText.FirstJsonObject(result.Text)
            ?? throw new InvalidOperationException("El agente de casos de prueba no devolvió JSON válido.");
        TestCaseReply reply = JsonSerializer.Deserialize<TestCaseReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente de casos de prueba devolvió una respuesta vacía.");
        if (string.IsNullOrWhiteSpace(reply.Titulo))
        {
            throw new InvalidOperationException("El agente de casos de prueba no devolvió título.");
        }

        return TestCase.Create(reply.Titulo!, reply.Precondiciones ?? new List<string>(),
            reply.Pasos ?? new List<string>(), reply.ResultadoEsperado ?? string.Empty);
    }

    private sealed record TestCaseReply(string? Titulo, List<string>? Precondiciones, List<string>? Pasos, string? ResultadoEsperado);
}
