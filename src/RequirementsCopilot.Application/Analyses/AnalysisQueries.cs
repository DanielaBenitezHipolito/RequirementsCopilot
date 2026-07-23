using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public sealed record TestCaseDetailDto(string Titulo, IReadOnlyList<string> Precondiciones, IReadOnlyList<string> Pasos,
    string ResultadoEsperado);

public sealed record StoryDetailDto(string Rol, string Quiero, string Para, IReadOnlyList<string> CriteriosAceptacion,
    TestCaseDetailDto? Caso);

public sealed record ClarificationDto(string Pregunta, string? Respuesta);

public sealed record RequirementDetailDto(string Codigo, string Texto, string Area, EvaluationDto? Evaluacion,
    IReadOnlyList<ClarificationDto> Aclaraciones, bool ListoParaHistorias, IReadOnlyList<StoryDetailDto> Historias);

public sealed record AnalysisSummaryDto(Guid Id, string FileName, DateTime CreatedAt, string Status,
    int TotalRequerimientos, int Aprobados);

public sealed record AnalysisDetailDto(Guid Id, string FileName, DateTime CreatedAt, string Status, string? Error,
    string? Resumen, IReadOnlyList<RequirementDetailDto> Requerimientos);

public sealed class AnalysisQueries
{
    private readonly IAnalysisRepository _repository;

    public AnalysisQueries(IAnalysisRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<AnalysisSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Analysis> analyses = await _repository.GetAllAsync(cancellationToken);
        return analyses.Select(a => new AnalysisSummaryDto(
            a.Id, a.FileName, a.CreatedAt, a.Status.ToString(),
            a.Requirements.Count,
            a.Requirements.Count(r => r.Evaluation?.Passed == true))).ToArray();
    }

    public async Task<AnalysisDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Analysis? analysis = await _repository.GetByIdAsync(id, cancellationToken);
        if (analysis is null)
        {
            return null;
        }

        return new AnalysisDetailDto(analysis.Id, analysis.FileName, analysis.CreatedAt, analysis.Status.ToString(),
            analysis.Error, analysis.Summary,
            analysis.Requirements.Select(MapRequirement).ToArray());
    }

    public static RequirementDetailDto MapRequirement(Requirement r) => new(
        r.Code, r.Text, r.Area,
        r.Evaluation is null ? null : new EvaluationDto(
            r.Code,
            r.Evaluation.Scores.Select(s => new CriterionDto(s.Criterion, s.Score, s.Observation)).ToArray(),
            Math.Round(r.Evaluation.Average, 2), r.Evaluation.Threshold, r.Evaluation.Passed),
        r.Clarifications.Select(c => new ClarificationDto(c.Question, c.Answer)).ToArray(),
        r.ReadyForStories,
        r.Stories.Select(s => new StoryDetailDto(
            s.Role, s.Goal, s.Benefit, s.AcceptanceCriteria,
            s.TestCase is null ? null : new TestCaseDetailDto(
                s.TestCase.Title, s.TestCase.Preconditions, s.TestCase.Steps, s.TestCase.ExpectedResult)))
            .ToArray());
}
