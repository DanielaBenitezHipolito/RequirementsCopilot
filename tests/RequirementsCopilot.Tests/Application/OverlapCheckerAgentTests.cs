using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class OverlapCheckerAgentTests
{
    private static Requirement Req() => Requirement.Create("REQ-001", "El sistema debe registrar pagos", "Pagos");

    [Fact]
    public async Task CheckAsync_Solapado_DevuelveAdvertenciaConElCasoYElMotivo()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"solapado\":true,\"con\":\"Registrar pago\",\"motivo\":\"Ya cubre el registro de pagos.\"}",
        };
        var agent = new OverlapCheckerAgent(chat);

        string? warning = await agent.CheckAsync(Req(), "CASOS DE USO YA DEFINIDOS…");

        Assert.NotNull(warning);
        Assert.Contains("Registrar pago", warning);
        Assert.Contains("Ya cubre el registro de pagos.", warning);
        Assert.Contains("El sistema debe registrar pagos", Assert.Single(chat.Prompts).Input);
    }

    [Fact]
    public async Task CheckAsync_SinSolapamiento_DevuelveNull()
    {
        var chat = new StubChatCompletion { Reply = _ => "{\"solapado\":false}" };
        var agent = new OverlapCheckerAgent(chat);
        Assert.Null(await agent.CheckAsync(Req(), "contexto"));
    }

    [Fact]
    public async Task CheckAsync_RespuestaInvalida_DevuelveNullSinTumbar()
    {
        var chat = new StubChatCompletion { Reply = _ => "no soy json" };
        var agent = new OverlapCheckerAgent(chat);
        Assert.Null(await agent.CheckAsync(Req(), "contexto"));
    }
}
