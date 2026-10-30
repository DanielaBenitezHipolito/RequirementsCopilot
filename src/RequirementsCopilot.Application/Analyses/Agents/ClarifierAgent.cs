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
    public async Task<IReadOnlyList<ClarifierQuestion>> AskAsync(Requirement requirement, string? projectContext = null,
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
        return Parse(json);
    }

    /// <summary>
    /// Acepta ambos contratos del agente: preguntas como textos planos (formato legado) o como
    /// objetos <c>{pregunta, opciones}</c>; los elementos irreconocibles se omiten.
    /// </summary>
    private static IReadOnlyList<ClarifierQuestion> Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("preguntas", out JsonElement preguntas) ||
            preguntas.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ClarifierQuestion>();
        }

        List<ClarifierQuestion> questions = new();
        foreach (JsonElement item in preguntas.EnumerateArray())
        {
            string? text = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Object when item.TryGetProperty("pregunta", out JsonElement p) => p.GetString(),
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }
            string[] options = item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("opciones", out JsonElement ops) && ops.ValueKind == JsonValueKind.Array
                    ? ops.EnumerateArray()
                        .Where(o => o.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(o.GetString()))
                        .Select(o => o.GetString()!.Trim())
                        .ToArray()
                    : Array.Empty<string>();
            questions.Add(new ClarifierQuestion(text.Trim(), options));
        }
        return questions;
    }
}
