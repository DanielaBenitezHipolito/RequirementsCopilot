using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

/// <summary>Redacta el caso de uso de un requerimiento siguiendo la plantilla corporativa.</summary>
public sealed class UseCaseWriterAgent
{
    /// <summary>Nombre lógico del agente, usado para mapear el prompt/agente publicado.</summary>
    public const string AgentName = "use-case-writer-agent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    /// <summary>Crea el agente sobre el puerto de chat dado.</summary>
    public UseCaseWriterAgent(IChatCompletion chat) => _chat = chat;

    /// <summary>Genera el caso de uso del requerimiento, incorporando sus aclaraciones respondidas si existen.</summary>
    public async Task<UseCase> WriteAsync(Requirement requirement, CancellationToken cancellationToken = default)
    {
        var answered = requirement.Clarifications.Where(c => c.IsAnswered).ToArray();
        string clarifications = answered.Length == 0
            ? string.Empty
            : "\n\nAclaraciones del cliente (úsalas para no malinterpretar):\n" +
              string.Join("\n", answered.Select(c => $"- P: {c.Question} R: {c.Answer}"));
        string json = await _chat.CompleteJsonAsync(
            new ChatPrompt(AgentName,
                $"Requerimiento {requirement.Code} (área {requirement.Area}):\n{requirement.Text}{clarifications}"),
            cancellationToken)
            ?? throw new InvalidOperationException("El agente de casos de uso no devolvió JSON válido.");
        UseCaseReply reply = JsonSerializer.Deserialize<UseCaseReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente de casos de uso devolvió una respuesta vacía.");

        return UseCase.Create(
            reply.Nombre ?? string.Empty,
            reply.Objetivo ?? string.Empty,
            reply.Descripcion,
            reply.Actores?.Select(a => new Actor(a.Nombre ?? string.Empty, a.Descripcion ?? string.Empty)).ToArray(),
            reply.Precondiciones,
            reply.Trigger,
            reply.Flujos?.Select(f => new FlujoProceso(
                f.Titulo ?? string.Empty,
                (f.Pasos ?? new List<PasoReply>())
                    .Select(p => new PasoFlujo(p.Numero, p.Accion ?? string.Empty, p.ResultadoEsperado ?? string.Empty))
                    .ToArray()))
                .ToArray(),
            reply.Extensiones,
            reply.Frecuencia,
            reply.Importancia,
            reply.Urgencia,
            reply.Comentarios);
    }

    private sealed record UseCaseReply(string? Nombre, string? Objetivo, string? Descripcion,
        List<ActorReply>? Actores, List<string>? Precondiciones, string? Trigger, List<FlujoReply>? Flujos,
        List<string>? Extensiones, string? Frecuencia, string? Importancia, string? Urgencia,
        List<string>? Comentarios);

    private sealed record ActorReply(string? Nombre, string? Descripcion);

    private sealed record FlujoReply(string? Titulo, List<PasoReply>? Pasos);

    private sealed record PasoReply(int Numero, string? Accion, string? ResultadoEsperado);
}
