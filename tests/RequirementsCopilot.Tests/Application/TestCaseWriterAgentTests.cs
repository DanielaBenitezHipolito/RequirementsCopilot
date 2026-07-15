using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class TestCaseWriterAgentTests
{
    private static UserStory Story()
        => UserStory.Create("cajero", "registrar un pago", "cerrar la venta", new[] { "dado un monto válido, queda registrado" });

    [Fact]
    public async Task WriteAsync_DevuelveCasoDePrueba()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"titulo\":\"Pago exitoso\",\"precondiciones\":[\"sesión de caja activa\"]," +
                "\"pasos\":[\"abrir caja\",\"registrar monto\"],\"resultadoEsperado\":\"pago registrado con consecutivo\"}",
        };
        var agent = new TestCaseWriterAgent(chat);

        var testCase = await agent.WriteAsync(Story());

        Assert.Equal("Pago exitoso", testCase.Title);
        Assert.Equal(2, testCase.Steps.Count);
        Assert.Equal("test-case-writer-agent", chat.Prompts[0].Agent);
        Assert.Contains("registrar un pago", chat.Prompts[0].Input);
    }

    [Fact]
    public async Task WriteAsync_SinJson_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "nada" };
        var agent = new TestCaseWriterAgent(chat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.WriteAsync(Story()));
    }
}
