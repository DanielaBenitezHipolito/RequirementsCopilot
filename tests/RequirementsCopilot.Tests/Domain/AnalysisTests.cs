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
    public void ReadyForStories_ExigeAprobarOResponderClarificaciones()
    {
        var requirement = Requirement.Create("REQ-010", "El sistema debe ser rápido", "General");
        Assert.False(requirement.ReadyForStories); // sin evaluación

        requirement.Evaluate(Evaluation.Create(new[] { CriterionScore.Create("Claridad", 2, "ambiguo") }, 3.5));
        Assert.False(requirement.ReadyForStories); // no pasa y sin preguntas respondidas

        requirement.AddClarification(Clarification.Create("¿Qué significa rápido?"));
        Assert.False(requirement.ReadyForStories); // pregunta sin responder

        requirement.Clarifications[0].Respond("Menos de 2 segundos");
        Assert.True(requirement.ReadyForStories); // clarificado
    }

    [Fact]
    public void FlujoCompleto_RequerimientoConCasoDeUso()
    {
        var analysis = Analysis.Create("spec.txt");
        var requirement = Requirement.Create("REQ-001", "El sistema debe X", "Pagos");
        requirement.Evaluate(Evaluation.Create(new[] { CriterionScore.Create("Claridad", 5, "ok") }, 3.5));
        var useCase = UseCase.Create("Módulo de Pólizas – Sistema HC Consulting", "Registrar pago", "Descripción",
            new[] { new Actor("Cajero", "Registra el pago") },
            new[] { "Sesión activa" }, "El huésped paga",
            new[] { new FlujoProceso("Proceso de creación manual", new[] { new PasoFlujo(1, "Abrir caja", "Pago registrado") }) },
            Array.Empty<string>(), "Única", "Alta", "Alta", Array.Empty<string>());
        requirement.SetUseCase(useCase);
        analysis.AddRequirement(requirement);
        analysis.Complete();

        Assert.Equal(AnalysisStatus.Completed, analysis.Status);
        Assert.Single(analysis.Requirements);
        Assert.True(analysis.Requirements[0].Evaluation!.Passed);
        Assert.NotNull(analysis.Requirements[0].UseCase);
    }
}
