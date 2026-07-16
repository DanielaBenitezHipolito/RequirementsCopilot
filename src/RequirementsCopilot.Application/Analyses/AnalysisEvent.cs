using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public enum AnalysisEventKind { Status, Requirement, Evaluation, Clarification, Story, TestCase, Done, Error }

public sealed record CriterionDto(string Nombre, int Score, string Observacion);

public sealed record RequirementDto(string Codigo, string Texto, string Area);

public sealed record EvaluationDto(string RequirementCode, IReadOnlyList<CriterionDto> Criterios, double Promedio, double Umbral, bool Pasa);

public sealed record ClarificationEventDto(string RequirementCode, IReadOnlyList<string> Preguntas);

public sealed record StoryDto(string RequirementCode, int StoryIndex, string Rol, string Quiero, string Para,
    IReadOnlyList<string> CriteriosAceptacion);

public sealed record TestCaseDto(string RequirementCode, int StoryIndex, string Titulo,
    IReadOnlyList<string> Precondiciones, IReadOnlyList<string> Pasos, string ResultadoEsperado);

public sealed record AnalysisEvent(AnalysisEventKind Kind)
{
    public string? Message { get; init; }
    public RequirementDto? Requirement { get; init; }
    public EvaluationDto? Evaluation { get; init; }
    public ClarificationEventDto? Clarification { get; init; }
    public StoryDto? Story { get; init; }
    public TestCaseDto? TestCase { get; init; }
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

    public static AnalysisEvent FromStory(string requirementCode, int storyIndex, UserStory story) => new(AnalysisEventKind.Story)
    {
        Story = new StoryDto(requirementCode, storyIndex, story.Role, story.Goal, story.Benefit, story.AcceptanceCriteria),
    };

    public static AnalysisEvent FromTestCase(string requirementCode, int storyIndex, TestCase testCase) => new(AnalysisEventKind.TestCase)
    {
        TestCase = new TestCaseDto(requirementCode, storyIndex, testCase.Title, testCase.Preconditions,
            testCase.Steps, testCase.ExpectedResult),
    };

    public static AnalysisEvent Done(Guid analysisId) => new(AnalysisEventKind.Done) { AnalysisId = analysisId };

    public static AnalysisEvent Error(string message) => new(AnalysisEventKind.Error) { Message = message };
}
