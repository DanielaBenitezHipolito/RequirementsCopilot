using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Domain;

public class AnalysisTests
{
    [Fact]
    public void Create_IniciaEnProcessing()
    {
        var analysis = Analysis.Create("spec.pdf");
        Assert.Equal(AnalysisStatus.Processing, analysis.Status);
        Assert.NotEqual(Guid.Empty, analysis.Id);
        Assert.Empty(analysis.Requirements);
    }

    [Fact]
    public void Fail_GuardaErrorYEstado()
    {
        var analysis = Analysis.Create("spec.pdf");
        analysis.Fail("LLM no disponible");
        Assert.Equal(AnalysisStatus.Failed, analysis.Status);
        Assert.Equal("LLM no disponible", analysis.Error);
    }

    [Fact]
    public void FlujoCompleto_RequerimientoConHistoriaYCaso()
    {
        var analysis = Analysis.Create("spec.txt");
        var requirement = Requirement.Create("REQ-001", "El sistema debe X", "Pagos");
        requirement.Evaluate(Evaluation.Create(new[] { CriterionScore.Create("Claridad", 5, "ok") }, 3.5));
        var story = UserStory.Create("cajero", "registrar pago", "cerrar la venta", new[] { "dado A entonces B" });
        story.AttachTestCase(TestCase.Create("Pago exitoso", new[] { "sesión activa" }, new[] { "abrir caja" }, "pago registrado"));
        requirement.AddStory(story);
        analysis.AddRequirement(requirement);
        analysis.Complete();

        Assert.Equal(AnalysisStatus.Completed, analysis.Status);
        Assert.Single(analysis.Requirements);
        Assert.True(analysis.Requirements[0].Evaluation!.Passed);
        Assert.NotNull(analysis.Requirements[0].Stories[0].TestCase);
    }
}
