using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Domain;

public class EvaluationTests
{
    private static CriterionScore Score(int value) => CriterionScore.Create("Claridad", value, "obs");

    [Fact]
    public void Create_PromedioSobreUmbral_Pasa()
    {
        var evaluation = Evaluation.Create(new[] { Score(4), Score(4), Score(3) }, threshold: 3.5);
        Assert.Equal(3.67, Math.Round(evaluation.Average, 2));
        Assert.True(evaluation.Passed);
    }

    [Fact]
    public void Create_PromedioBajoUmbral_NoPasa()
    {
        var evaluation = Evaluation.Create(new[] { Score(3), Score(3) }, threshold: 3.5);
        Assert.False(evaluation.Passed);
    }

    [Fact]
    public void Create_SinScores_Lanza()
        => Assert.Throws<ArgumentException>(() => Evaluation.Create(Array.Empty<CriterionScore>(), 3.5));

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void CriterionScore_FueraDeRango_Lanza(int score)
        => Assert.Throws<ArgumentOutOfRangeException>(() => CriterionScore.Create("Claridad", score, "obs"));
}
