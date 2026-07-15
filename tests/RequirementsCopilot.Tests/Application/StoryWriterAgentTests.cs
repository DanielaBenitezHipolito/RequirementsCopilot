using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class StoryWriterAgentTests
{
    [Fact]
    public async Task WriteAsync_DevuelveHistoriasConCriterios()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"historias\":[{\"rol\":\"cajero\",\"quiero\":\"registrar un pago\",\"para\":\"cerrar la venta\"," +
                "\"criteriosAceptacion\":[\"dado un monto válido, el pago queda registrado\"]}]}",
        };
        var agent = new StoryWriterAgent(chat);
        var requirement = Requirement.Create("REQ-001", "El sistema debe registrar pagos", "Pagos");

        var stories = await agent.WriteAsync(requirement);

        var story = Assert.Single(stories);
        Assert.Equal("cajero", story.Role);
        Assert.Equal("registrar un pago", story.Goal);
        Assert.Single(story.AcceptanceCriteria);
        Assert.Equal("story-writer-agent", chat.Prompts[0].Agent);
    }

    [Fact]
    public async Task WriteAsync_SinJson_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "..." };
        var agent = new StoryWriterAgent(chat);
        var requirement = Requirement.Create("REQ-001", "texto", "General");
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.WriteAsync(requirement));
    }
}
