using System.Text;
using RequirementsCopilot.Application.Analyses;
using UglyToad.PdfPig;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class PdfTextExtractor : IDocumentTextExtractor
{
    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        using (var document = PdfDocument.Open(content))
        {
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.AppendLine(page.Text);
            }
        }
        return Task.FromResult(builder.ToString());
    }
}
