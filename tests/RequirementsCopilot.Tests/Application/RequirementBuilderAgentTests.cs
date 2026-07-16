using RequirementsCopilot.Application.Analyses.Agents;

namespace RequirementsCopilot.Tests.Application;

public class RequirementBuilderAgentTests
{
    [Fact]
    public async Task ChatAsync_TurnoDePregunta_NoEstaListoYPropagaElHilo()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"listo\":false,\"mensaje\":\"¿Quién lo usa?\",\"requerimiento\":null}",
            ResponseId = "resp_1",
        };
        var agent = new RequirementBuilderAgent(chat);

        var turn = await agent.ChatAsync("Quiero controlar pagos", previousResponseId: null);

        Assert.False(turn.Listo);
        Assert.Equal("¿Quién lo usa?", turn.Mensaje);
        Assert.Null(turn.Texto);
        Assert.Equal("resp_1", turn.ResponseId);
        Assert.Null(chat.Prompts[0].PreviousResponseId);
    }

    [Fact]
    public async Task ChatAsync_TurnoListo_DevuelveBorradorYUsaElHiloPrevio()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"listo\":true,\"mensaje\":\"Listo\",\"requerimiento\":{\"texto\":\"El sistema debe X\",\"area\":\"Pagos\"}}",
            ResponseId = "resp_2",
        };
        var agent = new RequirementBuilderAgent(chat);

        var turn = await agent.ChatAsync("El cajero", previousResponseId: "resp_1");

        Assert.True(turn.Listo);
        Assert.Equal("El sistema debe X", turn.Texto);
        Assert.Equal("Pagos", turn.Area);
        Assert.Equal("resp_1", chat.Prompts[0].PreviousResponseId);
    }

    [Fact]
    public async Task ChatAsync_ListoSinTexto_SeTrataComoNoListo()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"listo\":true,\"mensaje\":\"hmm\",\"requerimiento\":{\"texto\":\"\",\"area\":\"X\"}}",
        };
        var turn = await new RequirementBuilderAgent(chat).ChatAsync("idea", null);
        Assert.False(turn.Listo);
    }

    [Fact]
    public async Task ChatAsync_SinJson_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "no json" };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new RequirementBuilderAgent(chat).ChatAsync("idea", null));
    }
}
