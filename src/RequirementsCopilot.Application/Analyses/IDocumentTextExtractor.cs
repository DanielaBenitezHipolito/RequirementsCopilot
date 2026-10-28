namespace RequirementsCopilot.Application.Analyses;

public interface IDocumentTextExtractor
{
    Task<DocumentContent> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}
