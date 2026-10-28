using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class ImageAttachmentTests
{
    [Fact]
    public void ToDataUrl_ComponeMimeYBase64()
    {
        var image = new ImageAttachment("image/png", "AAAA");
        Assert.Equal("data:image/png;base64,AAAA", image.ToDataUrl());
    }

    [Fact]
    public void FromDataUrl_DataUrlValida_SeparaMimeYBase64()
    {
        ImageAttachment image = ImageAttachment.FromDataUrl("data:image/jpeg;base64,/9j/AAAA");
        Assert.Equal("image/jpeg", image.MimeType);
        Assert.Equal("/9j/AAAA", image.Base64Data);
    }

    [Theory]
    [InlineData("")]
    [InlineData("data:image/png;base64,")]
    [InlineData("data:text/plain;base64,AAAA")]
    [InlineData("data:image/png,AAAA")]
    [InlineData("https://example.com/foto.png")]
    public void FromDataUrl_Invalida_Lanza(string dataUrl)
    {
        Assert.Throws<ArgumentException>(() => ImageAttachment.FromDataUrl(dataUrl));
    }

    [Fact]
    public void FromBytes_CodificaBase64()
    {
        ImageAttachment image = ImageAttachment.FromBytes("image/png", new byte[] { 1, 2, 3 });
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3 }), image.Base64Data);
    }
}
