using System.Text.Json;

namespace RequirementsCopilot.Application.Analyses.Agents;

/// <summary>Turno del entrevistador: mensaje al usuario, borrador si ya está listo, e id para continuar el hilo.</summary>
public sealed record BuilderTurn(string Mensaje, bool Listo, string? Texto, string? Area, string? ResponseId);

/// <summary>
/// Agente conversacional que construye un requerimiento a partir de la idea del usuario.
/// El hilo se mantiene con <c>previousResponseId</c> (Responses API); el agente no guarda estado local.
/// </summary>
public sealed class RequirementBuilderAgent
{
    public const string AgentName = "requirement-builder-agent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public RequirementBuilderAgent(IChatCompletion chat) => _chat = chat;

    /// <summary>
    /// Un turno del entrevistador. <paramref name="projectContext"/> se antepone solo al primer turno
    /// (sin <paramref name="previousResponseId"/>): el hilo lo conserva en los siguientes.
    /// </summary>
    public async Task<BuilderTurn> ChatAsync(string userMessage, string? previousResponseId,
        string? projectContext = null, CancellationToken cancellationToken = default)
    {
        string input = previousResponseId is null ? $"{projectContext}{userMessage}" : userMessage;
        ChatResult result = await _chat.CompleteAsync(
            new ChatPrompt(AgentName, input, previousResponseId), cancellationToken);

        string json = JsonText.FirstJsonObject(result.Text)
            ?? throw new InvalidOperationException("El agente entrevistador no devolvió JSON válido.");
        BuilderReply reply = JsonSerializer.Deserialize<BuilderReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente entrevistador devolvió una respuesta vacía.");

        if (string.IsNullOrWhiteSpace(reply.Mensaje))
        {
            throw new InvalidOperationException("El agente entrevistador devolvió un mensaje vacío.");
        }

        bool ready = reply.Listo && !string.IsNullOrWhiteSpace(reply.Requerimiento?.Texto);
        return new BuilderTurn(
            reply.Mensaje,
            ready,
            ready ? reply.Requerimiento!.Texto!.Trim() : null,
            ready ? reply.Requerimiento!.Area?.Trim() : null,
            result.ResponseId);
    }

    private sealed record BuilderReply(bool Listo, string? Mensaje, DraftRequirement? Requerimiento);

    private sealed record DraftRequirement(string? Texto, string? Area);
}
