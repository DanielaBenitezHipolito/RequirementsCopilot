using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class RequirementEvaluatorAgentTests
{
    private static Requirement Req() => Requirement.Create("REQ-001", "El sistema debe registrar pagos", "Pagos");

    [Fact]
    public async Task EvaluateAsync_RubricaCompleta_CalculaVeredicto()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"criterios\":[" +
                "{\"nombre\":\"Claridad\",\"score\":4,\"observacion\":\"clara\"}," +
                "{\"nombre\":\"Completitud\",\"score\":4,\"observacion\":\"completa\"}," +
                "{\"nombre\":\"Verificabilidad\",\"score\":4,\"observacion\":\"medible\"}," +
                "{\"nombre\":\"Consistencia\",\"score\":4,\"observacion\":\"consistente\"}," +
                "{\"nombre\":\"Factibilidad\",\"score\":4,\"observacion\":\"viable\"}]}",
        };
        var agent = new RequirementEvaluatorAgent(chat);

        var evaluation = await agent.EvaluateAsync(Req(), threshold: 3.5);

        Assert.Equal(5, evaluation.Scores.Count);
        Assert.Equal(4.0, evaluation.Average);
        Assert.True(evaluation.Passed);
        Assert.Equal("requirement-evaluator-agent", chat.Prompts[0].Agent);
        Assert.Contains("El sistema debe registrar pagos", chat.Prompts[0].Input);
    }

    [Fact]
    public async Task EvaluateAsync_ScoresBajos_NoPasa()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"criterios\":[{\"nombre\":\"Claridad\",\"score\":2,\"observacion\":\"ambigua\"}]}",
        };
        var agent = new RequirementEvaluatorAgent(chat);
        var evaluation = await agent.EvaluateAsync(Req(), threshold: 3.5);
        Assert.False(evaluation.Passed);
    }

    [Fact]
    public async Task EvaluateAsync_SinJson_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "sin datos" };
        var agent = new RequirementEvaluatorAgent(chat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.EvaluateAsync(Req(), 3.5));
    }
}
