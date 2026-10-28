using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class CompositeTextExtractor : IDocumentTextExtractor
{
    private readonly PlainTextExtractor _plain = new();
    private readonly PdfTextExtractor _pdf = new();
    private readonly DocxTextExtractor _docx = new();
    private readonly ImageFileExtractor _image = new();

    public Task<DocumentContent> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        IDocumentTextExtractor extractor = extension switch
        {
            ".txt" or ".md" => _plain,
            ".pdf" => _pdf,
            ".docx" => _docx,
            _ when ImageFileExtractor.MimeTypesByExtension.ContainsKey(extension) => _image,
            _ => throw new NotSupportedException(
                $"Formato no soportado: {extension}. Use PDF, DOCX, TXT, MD o una imagen (PNG, JPG, WEBP, GIF)."),
        };
        return extractor.ExtractAsync(content, fileName, cancellationToken);
    }
}
