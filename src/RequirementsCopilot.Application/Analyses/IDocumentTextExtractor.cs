namespace RequirementsCopilot.Application.Analyses;

public interface IDocumentTextExtractor
{
    Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}
