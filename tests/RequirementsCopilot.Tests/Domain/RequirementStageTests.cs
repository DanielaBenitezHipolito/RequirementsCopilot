using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Domain;

public class RequirementStageTests
{
    private static Requirement Req() => Requirement.Create("REQ-001", "El sistema debe X", "General");

    private static Evaluation Passing() =>
        Evaluation.Create(new[] { CriterionScore.Create("Claridad", 5, "ok") }, 3.5);

    private static Evaluation Failing() =>
        Evaluation.Create(new[] { CriterionScore.Create("Claridad", 1, "ambiguo") }, 3.5);

    [Fact]
    public void Create_ArrancaEnBorrador()
    {
        Assert.Equal(RequirementStage.Draft, Req().Stage);
    }

    [Fact]
    public void Evaluate_Aprobado_PasaAAprobado()
    {
        var requirement = Req();
        requirement.Evaluate(Passing());
        Assert.Equal(RequirementStage.Approved, requirement.Stage);
    }

    [Fact]
    public void Evaluate_NoAprobado_SigueEnBorrador()
    {
        var requirement = Req();
        requirement.Evaluate(Failing());
        Assert.Equal(RequirementStage.Draft, requirement.Stage);
    }

    [Fact]
    public void Evaluate_NoRetrocedeUnaEtapaAvanzada()
    {
        var requirement = Req();
        requirement.SetStage(RequirementStage.InDevelopment);
        requirement.Evaluate(Passing());
        Assert.Equal(RequirementStage.InDevelopment, requirement.Stage);
    }

    [Fact]
    public void SetStage_CambiaLaEtapa()
    {
        var requirement = Req();
        requirement.SetStage(RequirementStage.Implemented);
        Assert.Equal(RequirementStage.Implemented, requirement.Stage);
    }
}
