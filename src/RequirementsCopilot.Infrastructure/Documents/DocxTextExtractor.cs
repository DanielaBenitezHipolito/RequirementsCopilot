using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class DocxTextExtractor : IDocumentTextExtractor
{
    public Task<DocumentContent> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        var images = new List<ImageAttachment>();
        using (var document = WordprocessingDocument.Open(content, isEditable: false))
        {
            MainDocumentPart? main = document.MainDocumentPart;
            Body? body = main?.Document.Body;
            if (body is not null)
            {
                foreach (Paragraph paragraph in body.Descendants<Paragraph>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    builder.AppendLine(paragraph.InnerText);
                }
            }
            // ponytail: solo imágenes del cuerpo principal; headers/footers casi nunca traen mockups.
            foreach (ImagePart part in main?.ImageParts ?? Enumerable.Empty<ImagePart>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ImageFileExtractor.MimeTypesByExtension.Values.Contains(part.ContentType))
                {
                    continue; // formatos que la visión del modelo no acepta (emf, wmf, tiff…)
                }
                using var imageStream = new MemoryStream();
                part.GetStream().CopyTo(imageStream);
                images.Add(ImageAttachment.FromBytes(part.ContentType, imageStream.ToArray()));
            }
        }
        return Task.FromResult(new DocumentContent(builder.ToString(), images));
    }
}
