using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

/// <summary>
/// Redacta el backlog de historias de usuario (con puntos Fibonacci) de un requerimiento listo.
/// Ver ADR-0006: se invoca manualmente, después de evaluar y clarificar, nunca en la entrevista.
/// </summary>
public sealed class UserStoryWriterAgent
{
    /// <summary>Nombre lógico del agente, usado para mapear el prompt/agente publicado.</summary>
    public const string AgentName = "user-story-writer-agent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    /// <summary>Crea el agente sobre el puerto de chat dado.</summary>
    public UserStoryWriterAgent(IChatCompletion chat) => _chat = chat;

    /// <summary>Genera las historias del requerimiento, usando aclaraciones, caso de uso y contexto de proyecto.</summary>
    public async Task<IReadOnlyList<UserStory>> WriteAsync(Requirement requirement, string? projectContext = null,
        CancellationToken cancellationToken = default)
    {
        var answered = requirement.Clarifications.Where(c => c.IsAnswered).ToArray();
        string clarifications = answered.Length == 0
            ? string.Empty
            : "\n\nAclaraciones del cliente:\n" +
              string.Join("\n", answered.Select(c => $"- P: {c.Question} R: {c.Answer}"));

        string useCase = requirement.UseCase is null
            ? string.Empty
            : $"\n\nCaso de uso ya redactado (mantén las historias consistentes con él):\n" +
              $"Nombre: {requirement.UseCase.Nombre}\nObjetivo: {requirement.UseCase.Objetivo}\n" +
              $"Descripción: {requirement.UseCase.Descripcion}";

        string json = await _chat.CompleteJsonAsync(
            new ChatPrompt(AgentName,
                $"{projectContext}Requerimiento {requirement.Code} (área {requirement.Area}):\n" +
                $"{requirement.Text}{clarifications}{useCase}"),
            cancellationToken)
            ?? throw new InvalidOperationException("El agente de historias no devolvió JSON válido.");
        StoriesReply reply = JsonSerializer.Deserialize<StoriesReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente de historias devolvió una respuesta vacía.");

        UserStory[] stories = (reply.Historias ?? new List<StoryReply>())
            .Where(h => !string.IsNullOrWhiteSpace(h.Titulo) && UserStory.PuntosValidos.Contains(h.Puntos))
            .Select(h => UserStory.Create(h.Titulo!, h.Como, h.Quiero, h.Para,
                h.CriteriosAceptacion, h.Puntos, h.Dependencias))
            .ToArray();
        if (stories.Length == 0)
        {
            throw new InvalidOperationException("El agente de historias no devolvió historias válidas.");
        }
        return stories;
    }

    private sealed record StoriesReply(List<StoryReply>? Historias);

    private sealed record StoryReply(string? Titulo, string? Como, string? Quiero, string? Para,
        List<string>? CriteriosAceptacion, int Puntos, List<string>? Dependencias);
}
