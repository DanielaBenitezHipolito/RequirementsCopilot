using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class DocxTextExtractor : IDocumentTextExtractor
{
    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        using (var document = WordprocessingDocument.Open(content, isEditable: false))
        {
            Body? body = document.MainDocumentPart?.Document.Body;
            if (body is not null)
            {
                foreach (Paragraph paragraph in body.Descendants<Paragraph>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    builder.AppendLine(paragraph.InnerText);
                }
            }
        }
        return Task.FromResult(builder.ToString());
    }
}
