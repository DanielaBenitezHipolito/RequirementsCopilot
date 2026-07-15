namespace RequirementsCopilot.Domain.Analyses;

public sealed record CriterionScore
{
    public string Criterion { get; }
    public int Score { get; }
    public string Observation { get; }

    private CriterionScore(string criterion, int score, string observation)
        => (Criterion, Score, Observation) = (criterion, score, observation);

    public static CriterionScore Create(string criterion, int score, string observation)
    {
        if (string.IsNullOrWhiteSpace(criterion))
        {
            throw new ArgumentException("El criterio no puede estar vacío.", nameof(criterion));
        }
        if (score is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "El score debe estar entre 1 y 5.");
        }
        return new CriterionScore(criterion.Trim(), score, observation?.Trim() ?? string.Empty);
    }
}
