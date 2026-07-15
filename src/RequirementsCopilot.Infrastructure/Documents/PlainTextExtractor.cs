using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class PlainTextExtractor : IDocumentTextExtractor
{
    public async Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
