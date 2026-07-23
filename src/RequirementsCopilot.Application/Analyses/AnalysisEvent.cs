using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public enum AnalysisEventKind { Status, Requirement, Evaluation, Clarification, Summary, Done, Error }

public sealed record CriterionDto(string Nombre, int Score, string Observacion);

public sealed record RequirementDto(string Codigo, string Texto, string Area);

public sealed record EvaluationDto(string RequirementCode, IReadOnlyList<CriterionDto> Criterios, double Promedio, double Umbral, bool Pasa);

public sealed record ClarificationEventDto(string RequirementCode, IReadOnlyList<string> Preguntas);

public sealed record AnalysisEvent(AnalysisEventKind Kind)
{
    public string? Message { get; init; }
    public RequirementDto? Requirement { get; init; }
    public EvaluationDto? Evaluation { get; init; }
    public ClarificationEventDto? Clarification { get; init; }
    public string? Summary { get; init; }
    public Guid? AnalysisId { get; init; }

    public static AnalysisEvent Status(string message) => new(AnalysisEventKind.Status) { Message = message };

    public static AnalysisEvent FromRequirement(Requirement requirement) => new(AnalysisEventKind.Requirement)
    {
        Requirement = new RequirementDto(requirement.Code, requirement.Text, requirement.Area),
    };

    public static AnalysisEvent FromEvaluation(Requirement requirement) => new(AnalysisEventKind.Evaluation)
    {
        Evaluation = new EvaluationDto(
            requirement.Code,
            requirement.Evaluation!.Scores.Select(s => new CriterionDto(s.Criterion, s.Score, s.Observation)).ToArray(),
            Math.Round(requirement.Evaluation.Average, 2),
            requirement.Evaluation.Threshold,
            requirement.Evaluation.Passed),
    };

    public static AnalysisEvent FromClarifications(Requirement requirement) => new(AnalysisEventKind.Clarification)
    {
        Clarification = new ClarificationEventDto(requirement.Code,
            requirement.Clarifications.Select(c => c.Question).ToArray()),
    };

    public static AnalysisEvent FromSummary(string summary) => new(AnalysisEventKind.Summary) { Summary = summary };

    public static AnalysisEvent Done(Guid analysisId) => new(AnalysisEventKind.Done) { AnalysisId = analysisId };

    public static AnalysisEvent Error(string message) => new(AnalysisEventKind.Error) { Message = message };
}
