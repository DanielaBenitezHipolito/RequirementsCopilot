using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class PlainTextExtractor : IDocumentTextExtractor
{
    public async Task<DocumentContent> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        return DocumentContent.FromText(await reader.ReadToEndAsync(cancellationToken));
    }
}
