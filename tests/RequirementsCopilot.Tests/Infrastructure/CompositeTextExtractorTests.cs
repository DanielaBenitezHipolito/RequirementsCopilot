using System.Text;
using RequirementsCopilot.Infrastructure.Documents;

namespace RequirementsCopilot.Tests.Infrastructure;

public class CompositeTextExtractorTests
{
    private readonly CompositeTextExtractor _extractor = new();

    [Theory]
    [InlineData("spec.txt")]
    [InlineData("spec.md")]
    [InlineData("SPEC.TXT")]
    public async Task ExtractAsync_TextoPlano_DevuelveContenido(string fileName)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("REQ-001: el sistema debe X"));
        Assert.Equal("REQ-001: el sistema debe X", await _extractor.ExtractAsync(stream, fileName));
    }

    [Fact]
    public async Task ExtractAsync_ExtensionNoSoportada_Lanza()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => _extractor.ExtractAsync(stream, "spec.xlsx"));
        Assert.Contains(".xlsx", ex.Message);
    }

    [Fact]
    public async Task ExtractAsync_Docx_ExtraeParrafos()
    {
        using var docx = BuildDocx("El sistema debe registrar pagos.");
        string text = await _extractor.ExtractAsync(docx, "spec.docx");
        Assert.Contains("El sistema debe registrar pagos.", text);
    }

    private static MemoryStream BuildDocx(string paragraph)
    {
        var stream = new MemoryStream();
        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                new DocumentFormat.OpenXml.Wordprocessing.Body(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                            new DocumentFormat.OpenXml.Wordprocessing.Text(paragraph)))));
        }
        stream.Position = 0;
        return stream;
    }
}
