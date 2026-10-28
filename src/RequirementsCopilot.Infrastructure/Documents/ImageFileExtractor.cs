using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Documents;

/// <summary>
/// "Extractor" para archivos que son una imagen suelta (captura, mockup, diagrama):
/// no hay texto que extraer, la imagen misma es el contenido a analizar.
/// </summary>
public sealed class ImageFileExtractor : IDocumentTextExtractor
{
    /// <summary>Extensiones de imagen soportadas y su tipo MIME (los formatos que acepta la visión del modelo).</summary>
    internal static readonly IReadOnlyDictionary<string, string> MimeTypesByExtension = new Dictionary<string, string>
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
    };

    public async Task<DocumentContent> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        string mimeType = MimeTypesByExtension[Path.GetExtension(fileName).ToLowerInvariant()];
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        return new DocumentContent(string.Empty,
            new[] { ImageAttachment.FromBytes(mimeType, buffer.ToArray()) });
    }
}
