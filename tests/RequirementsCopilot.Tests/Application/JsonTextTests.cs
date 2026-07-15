using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class JsonTextTests
{
    [Fact]
    public void FirstJsonObject_ConProsaYVallas_ExtraeObjeto()
    {
        const string text = "Claro, aquí está:\n```json\n{\"a\":1}\n```\ngracias";
        Assert.Equal("{\"a\":1}", JsonText.FirstJsonObject(text));
    }

    [Fact]
    public void FirstJsonObject_ObjetoDuplicado_TomaElPrimero()
        => Assert.Equal("{\"a\":1}", JsonText.FirstJsonObject("{\"a\":1}{\"a\":2}"));

    [Fact]
    public void FirstJsonObject_LlavesDentroDeCadenas_NoRompeBalance()
        => Assert.Equal("{\"a\":\"x}y\"}", JsonText.FirstJsonObject("{\"a\":\"x}y\"}"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin json")]
    [InlineData("{\"abierto\":1")]
    public void FirstJsonObject_SinObjetoCerrado_DevuelveNull(string? text)
        => Assert.Null(JsonText.FirstJsonObject(text));
}
