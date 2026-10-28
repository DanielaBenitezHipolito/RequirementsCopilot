using System.Text;
using DocumentFormat.OpenXml.Packaging;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Infrastructure.Documents;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace RequirementsCopilot.Tests.Infrastructure;

public class CompositeTextExtractorTests
{
    /// <summary>PNG válido de 1x1 px, para incrustar en docx/pdf de prueba.</summary>
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private static byte[] OnePixelPng => Convert.FromBase64String(OnePixelPngBase64);

    private readonly CompositeTextExtractor _extractor = new();

    [Theory]
    [InlineData("spec.txt")]
    [InlineData("spec.md")]
    [InlineData("SPEC.TXT")]
    public async Task ExtractAsync_TextoPlano_DevuelveContenido(string fileName)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("REQ-001: el sistema debe X"));
        DocumentContent content = await _extractor.ExtractAsync(stream, fileName);
        Assert.Equal("REQ-001: el sistema debe X", content.Text);
        Assert.Empty(content.Images);
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
        DocumentContent content = await _extractor.ExtractAsync(docx, "spec.docx");
        Assert.Contains("El sistema debe registrar pagos.", content.Text);
        Assert.Empty(content.Images);
    }

    [Fact]
    public async Task ExtractAsync_DocxConImagenEmbebida_DevuelveTextoEImagen()
    {
        using var docx = BuildDocx("Mockup de la pantalla:", OnePixelPng);
        DocumentContent content = await _extractor.ExtractAsync(docx, "spec.docx");
        Assert.Contains("Mockup de la pantalla:", content.Text);
        ImageAttachment image = Assert.Single(content.Images);
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(OnePixelPngBase64, image.Base64Data);
    }

    [Fact]
    public async Task ExtractAsync_PdfConImagen_DevuelveImagen()
    {
        using var pdf = BuildPdfWithImage(OnePixelPng);
        DocumentContent content = await _extractor.ExtractAsync(pdf, "scan.pdf");
        ImageAttachment image = Assert.Single(content.Images);
        Assert.Equal("image/png", image.MimeType);
    }

    /// <summary>JPEG válido de 1x1 px (DCTDecode), para el fallback de bytes crudos del PDF.</summary>
    private const string OnePixelJpegBase64 =
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCAABAAEBAREA/8QAFAABAAAAAAAAAAAAAAAAAAAACf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAD8AKp//2Q==";

    [Fact]
    public async Task ExtractAsync_PdfConJpeg_DevuelveImagenJpegOPng()
    {
        byte[] jpeg = Convert.FromBase64String(OnePixelJpegBase64);
        var builder = new PdfDocumentBuilder();
        PdfPageBuilder page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        page.AddJpeg(jpeg, new PdfRectangle(25, 500, 125, 600));
        using var pdf = new MemoryStream(builder.Build());

        DocumentContent content = await _extractor.ExtractAsync(pdf, "scan.pdf");

        ImageAttachment image = Assert.Single(content.Images);
        Assert.Contains(image.MimeType, new[] { "image/jpeg", "image/png" });
    }

    [Fact]
    public async Task ExtractAsync_DocxConImagenNoSoportada_LaOmite()
    {
        using var docx = BuildDocx("Texto.", pngImage: null, emfImage: new byte[] { 1, 2, 3 });
        DocumentContent content = await _extractor.ExtractAsync(docx, "spec.docx");
        Assert.Empty(content.Images);
    }

    [Theory]
    [InlineData("foto.png", "image/png")]
    [InlineData("foto.jpg", "image/jpeg")]
    [InlineData("foto.jpeg", "image/jpeg")]
    [InlineData("foto.webp", "image/webp")]
    [InlineData("foto.gif", "image/gif")]
    [InlineData("FOTO.PNG", "image/png")]
    public async Task ExtractAsync_ArchivoImagen_DevuelveImagenSinTexto(string fileName, string expectedMime)
    {
        using var stream = new MemoryStream(OnePixelPng);
        DocumentContent content = await _extractor.ExtractAsync(stream, fileName);
        Assert.Equal(string.Empty, content.Text);
        ImageAttachment image = Assert.Single(content.Images);
        Assert.Equal(expectedMime, image.MimeType);
        Assert.Equal(Convert.ToBase64String(OnePixelPng), image.Base64Data);
    }

    private static MemoryStream BuildDocx(string paragraph, byte[]? pngImage = null, byte[]? emfImage = null)
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
            if (pngImage is not null)
            {
                var imagePart = main.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Png);
                using var imageStream = new MemoryStream(pngImage);
                imagePart.FeedData(imageStream);
            }
            if (emfImage is not null)
            {
                var emfPart = main.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Emf);
                using var emfStream = new MemoryStream(emfImage);
                emfPart.FeedData(emfStream);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildPdfWithImage(byte[] pngImage)
    {
        var builder = new PdfDocumentBuilder();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        PdfPageBuilder page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        page.AddText("Pantalla:", 12, new PdfPoint(25, 700), font);
        page.AddPng(pngImage, new PdfRectangle(25, 500, 125, 600));
        return new MemoryStream(builder.Build());
    }
}
