using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

/// <summary>
/// Compara un requerimiento nuevo contra los casos de uso ya definidos del proyecto (que viajan en
/// el contexto) y avisa si lo que pide ya está cubierto. Best effort: una respuesta inválida del
/// modelo se trata como "sin solapamiento", nunca tumba el análisis.
/// </summary>
public sealed class OverlapCheckerAgent
{
    /// <summary>Código lógico del agente (archivo de prompt en /prompts).</summary>
    public const string AgentName = "overlap-checker-agent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    /// <summary>Crea el agente sobre el puerto de chat.</summary>
    public OverlapCheckerAgent(IChatCompletion chat) => _chat = chat;

    /// <summary>
    /// Advertencia de solapamiento (caso de uso existente + motivo), o null si no solapa o la
    /// respuesta del modelo no es interpretable.
    /// </summary>
    public async Task<string?> CheckAsync(Requirement requirement, string projectContext,
        CancellationToken cancellationToken = default)
    {
        string? json = await _chat.CompleteJsonAsync(new ChatPrompt(AgentName,
            $"{projectContext}Requerimiento nuevo {requirement.Code} (área {requirement.Area}):\n{requirement.Text}"),
            cancellationToken);
        if (json is null)
        {
            return null;
        }
        OverlapReply? reply;
        try
        {
            reply = JsonSerializer.Deserialize<OverlapReply>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        return reply is { Solapado: true } && !string.IsNullOrWhiteSpace(reply.Con)
            ? $"Posible solapamiento con el caso de uso «{reply.Con}»: {reply.Motivo ?? "revisar cobertura existente."}"
            : null;
    }

    private sealed record OverlapReply(bool Solapado, string? Con, string? Motivo);
}
