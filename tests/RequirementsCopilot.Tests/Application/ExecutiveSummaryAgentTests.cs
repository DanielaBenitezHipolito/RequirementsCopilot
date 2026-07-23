using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class ExecutiveSummaryAgentTests
{
    private static Analysis AnalysisConRequerimientos()
    {
        var analysis = Analysis.Create("spec.pdf");
        var aprobado = Requirement.Create("REQ-001", "El sistema debe registrar pagos", "Pagos");
        aprobado.Evaluate(Evaluation.Create(new[]
        {
            CriterionScore.Create("Claridad", 5, "clara"),
            CriterionScore.Create("Completitud", 4, "completa"),
        }, 3.5));
        analysis.AddRequirement(aprobado);

        var ambiguo = Requirement.Create("REQ-002", "El sistema debe ser rápido", "General");
        ambiguo.Evaluate(Evaluation.Create(new[]
        {
            CriterionScore.Create("Claridad", 1, "Termino subjetivo sin metrica."),
            CriterionScore.Create("Completitud", 2, "No define metricas."),
            CriterionScore.Create("Verificabilidad", 1, "No medible."),
        }, 3.5));
        analysis.AddRequirement(ambiguo);

        return analysis;
    }

    [Fact]
    public async Task SummarizeAsync_ConstruyeInputCompactoYDevuelveResumen()
    {
        var chat = new StubChatCompletion { Reply = _ => "{\"resumen\":\"Resumen ejecutivo de la auditoría.\"}" };
        var agent = new ExecutiveSummaryAgent(chat);

        string resumen = await agent.SummarizeAsync(AnalysisConRequerimientos());

        Assert.Equal("Resumen ejecutivo de la auditoría.", resumen);
        Assert.Equal("executive-summary-agent", chat.Prompts[0].Agent);
        string input = chat.Prompts[0].Input;
        Assert.Contains("REQ-001", input);
        Assert.Contains("REQ-002", input);
        Assert.Contains("Termino subjetivo sin metrica.", input); // observación de score <=3
        Assert.DoesNotContain("clara", input); // REQ-001 no tiene observaciones clave (scores >3)
    }

    [Fact]
    public async Task SummarizeAsync_SinJson_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "sin datos" };
        var agent = new ExecutiveSummaryAgent(chat);

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.SummarizeAsync(AnalysisConRequerimientos()));
    }
}
