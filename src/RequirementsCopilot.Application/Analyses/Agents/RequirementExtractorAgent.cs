using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class RequirementExtractorAgent
{
    public const string AgentName = "requirement-extractor-agent";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public RequirementExtractorAgent(IChatCompletion chat) => _chat = chat;

    public async Task<IReadOnlyList<Requirement>> ExtractAsync(string documentText,
        IReadOnlyList<ImageAttachment>? images = null, CancellationToken cancellationToken = default)
    {
        // El agente publicado en Foundry recibe el aviso de los adjuntos en el texto: sin él,
        // un documento escaneado (texto vacío) parecería no traer nada que extraer.
        string input = images is { Count: > 0 }
            ? $"Documento:\n{documentText}\n\nSe adjuntan {images.Count} imagen(es) que forman parte del MISMO " +
              "documento (pantallas, mockups o diagramas que ilustran su contenido). Léelas como complemento del " +
              "texto y respeta la REGLA CENTRAL de unidades completas: si el documento describe un único caso de " +
              "uso, las imágenes son pasos o pantallas de ESE caso de uso — NUNCA crees un requerimiento por imagen."
            : $"Documento:\n{documentText}";
        string json = await _chat.CompleteJsonAsync(
            new ChatPrompt(AgentName, input, Images: images), cancellationToken)
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
