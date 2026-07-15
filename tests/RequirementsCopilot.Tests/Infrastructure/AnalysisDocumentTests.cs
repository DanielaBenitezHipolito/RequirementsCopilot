using RequirementsCopilot.Domain.Analyses;
using RequirementsCopilot.Infrastructure.Mongo;

namespace RequirementsCopilot.Tests.Infrastructure;

public class AnalysisDocumentTests
{
    [Fact]
    public void FromDomain_ToDomain_RoundTripCompleto()
    {
        var analysis = Analysis.Create("spec.pdf");
        var requirement = Requirement.Create("REQ-001", "El sistema debe X", "Pagos");
        requirement.Evaluate(Evaluation.Create(
            new[] { CriterionScore.Create("Claridad", 4, "clara"), CriterionScore.Create("Completitud", 3, "parcial") }, 3.5));
        var story = UserStory.Create("cajero", "registrar pago", "cerrar venta", new[] { "dado A entonces B" });
        story.AttachTestCase(TestCase.Create("Pago ok", new[] { "caja abierta" }, new[] { "registrar" }, "registrado"));
        requirement.AddStory(story);
        analysis.AddRequirement(requirement);
        analysis.Complete();

        var restored = AnalysisDocument.FromDomain(analysis).ToDomain();

        Assert.Equal(analysis.Id, restored.Id);
        Assert.Equal(analysis.Status, restored.Status);
        Assert.Equal("REQ-001", restored.Requirements[0].Code);
        Assert.Equal("Pagos", restored.Requirements[0].Area);
        Assert.Equal(3.5, restored.Requirements[0].Evaluation!.Average);
        Assert.True(restored.Requirements[0].Evaluation!.Passed);
        Assert.Equal("Pago ok", restored.Requirements[0].Stories[0].TestCase!.Title);
    }

    [Fact]
    public void FromDomain_ToDomain_AnalisisFallido()
    {
        var analysis = Analysis.Create("spec.txt");
        analysis.Fail("LLM no disponible");
        var restored = AnalysisDocument.FromDomain(analysis).ToDomain();
        Assert.Equal(AnalysisStatus.Failed, restored.Status);
        Assert.Equal("LLM no disponible", restored.Error);
    }
}
