using RequirementsCopilot.Application.Analyses.Agents;

namespace RequirementsCopilot.Tests.Application;

public class RequirementExtractorAgentTests
{
    [Fact]
    public async Task ExtractAsync_JsonConProsa_DevuelveRequerimientos()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "Listo:\n{\"requerimientos\":[{\"codigo\":\"REQ-001\",\"texto\":\"El sistema debe registrar pagos\",\"area\":\"Pagos\"}]}",
        };
        var agent = new RequirementExtractorAgent(chat);

        var requirements = await agent.ExtractAsync("documento de prueba");

        var requirement = Assert.Single(requirements);
        Assert.Equal("REQ-001", requirement.Code);
        Assert.Equal("Pagos", requirement.Area);
        Assert.Equal("requirement-extractor-agent", chat.Prompts[0].Agent);
        Assert.Contains("documento de prueba", chat.Prompts[0].Input);
    }

    [Fact]
    public async Task ExtractAsync_SinJson_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "no puedo" };
        var agent = new RequirementExtractorAgent(chat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ExtractAsync("doc"));
    }

    [Fact]
    public async Task ExtractAsync_ListaVacia_DevuelveVacio()
    {
        var chat = new StubChatCompletion { Reply = _ => "{\"requerimientos\":[]}" };
        var agent = new RequirementExtractorAgent(chat);
        Assert.Empty(await agent.ExtractAsync("doc"));
    }
}
