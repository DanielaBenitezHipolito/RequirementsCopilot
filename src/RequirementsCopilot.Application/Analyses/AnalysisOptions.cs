namespace RequirementsCopilot.Application.Analyses;

public sealed record AnalysisOptions
{
    public double PassThreshold { get; init; } = 3.5;
}
