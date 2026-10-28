using System.Text;
using RequirementsCopilot.Application.Analyses;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class PdfTextExtractor : IDocumentTextExtractor
{
    public Task<DocumentContent> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        var images = new List<ImageAttachment>();
        using (var document = PdfDocument.Open(content))
        {
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.AppendLine(page.Text);
                foreach (IPdfImage image in page.GetImages())
                {
                    if (TryConvert(image, out ImageAttachment? attachment))
                    {
                        images.Add(attachment!);
                    }
                }
            }
        }
        return Task.FromResult(new DocumentContent(builder.ToString(), images));
    }

    /// <summary>
    /// Convierte una imagen del PDF a un formato que acepte la visión del modelo: PNG cuando PdfPig
    /// puede decodificarla, o los bytes crudos si ya son JPEG (DCTDecode). Otros códecs se omiten.
    /// </summary>
    private static bool TryConvert(IPdfImage image, out ImageAttachment? attachment)
    {
        if (image.TryGetPng(out byte[]? png) && png is not null)
        {
            attachment = ImageAttachment.FromBytes("image/png", png);
            return true;
        }
        byte[] raw = image.RawBytes.ToArray();
        if (raw.Length > 2 && raw[0] == 0xFF && raw[1] == 0xD8)
        {
            attachment = ImageAttachment.FromBytes("image/jpeg", raw);
            return true;
        }
        attachment = null;
        return false;
    }
}
