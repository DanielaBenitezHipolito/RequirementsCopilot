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
        var useCase = UseCase.Create("Módulo de Pólizas – Sistema HC Consulting", "Registrar pago", "Descripción",
            new[] { new Actor("Cajero", "Registra el pago") },
            new[] { "Reserva creada" }, "El huésped paga",
            new[] { new FlujoProceso("Proceso de creación manual", new[] { new PasoFlujo(1, "Abrir caja", "Caja abierta") }) },
            new[] { "Si el monto no coincide, se rechaza" }, "Única", "Alta", "Alta", new[] { "Ninguno" });
        requirement.SetUseCase(useCase);
        analysis.AddRequirement(requirement);
        analysis.Complete();

        var restored = AnalysisDocument.FromDomain(analysis).ToDomain();

        Assert.Equal(analysis.Id, restored.Id);
        Assert.Equal(analysis.Status, restored.Status);
        Assert.Equal("REQ-001", restored.Requirements[0].Code);
        Assert.Equal("Pagos", restored.Requirements[0].Area);
        Assert.Equal(3.5, restored.Requirements[0].Evaluation!.Average);
        Assert.True(restored.Requirements[0].Evaluation!.Passed);
        Assert.Equal("Módulo de Pólizas – Sistema HC Consulting", restored.Requirements[0].UseCase!.Nombre);
        Assert.Single(restored.Requirements[0].UseCase!.Actores);
        Assert.Single(restored.Requirements[0].UseCase!.Flujos);
        Assert.Single(restored.Requirements[0].UseCase!.Flujos[0].Pasos);
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
