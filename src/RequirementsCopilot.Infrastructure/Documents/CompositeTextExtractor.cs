using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class CompositeTextExtractor : IDocumentTextExtractor
{
    private readonly PlainTextExtractor _plain = new();
    private readonly PdfTextExtractor _pdf = new();
    private readonly DocxTextExtractor _docx = new();

    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        IDocumentTextExtractor extractor = extension switch
        {
            ".txt" or ".md" => _plain,
            ".pdf" => _pdf,
            ".docx" => _docx,
            _ => throw new NotSupportedException($"Formato no soportado: {extension}. Use PDF, DOCX, TXT o MD."),
        };
        return extractor.ExtractAsync(content, fileName, cancellationToken);
    }
}
