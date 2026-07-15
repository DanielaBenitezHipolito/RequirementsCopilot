namespace RequirementsCopilot.Domain.Analyses;

public sealed record Evaluation
{
    public IReadOnlyList<CriterionScore> Scores { get; }
    public double Threshold { get; }
    public double Average { get; }
    public bool Passed { get; }

    private Evaluation(IReadOnlyList<CriterionScore> scores, double threshold)
    {
        Scores = scores;
        Threshold = threshold;
        Average = scores.Average(s => s.Score);
        Passed = Average >= threshold;
    }

    public static Evaluation Create(IReadOnlyList<CriterionScore> scores, double threshold)
    {
        if (scores is null || scores.Count == 0)
        {
            throw new ArgumentException("La evaluación requiere al menos un criterio.", nameof(scores));
        }
        return new Evaluation(scores.ToArray(), threshold);
    }
}
