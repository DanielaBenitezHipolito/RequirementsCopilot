using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public sealed record ActorDto(string Nombre, string Descripcion);

public sealed record PasoFlujoDto(int Numero, string Accion, string ResultadoEsperado);

public sealed record FlujoDto(string Titulo, IReadOnlyList<PasoFlujoDto> Pasos);

public sealed record UseCaseDto(string Nombre, string Objetivo, string Descripcion, IReadOnlyList<ActorDto> Actores,
    IReadOnlyList<string> Precondiciones, string Trigger, IReadOnlyList<FlujoDto> Flujos,
    IReadOnlyList<string> Extensiones, string Frecuencia, string Importancia, string Urgencia,
    IReadOnlyList<string> Comentarios);

public sealed record ClarificationDto(string Pregunta, string? Respuesta);

public sealed record RequirementDetailDto(string Codigo, string Texto, string Area, EvaluationDto? Evaluacion,
    IReadOnlyList<ClarificationDto> Aclaraciones, bool ListoParaHistorias, UseCaseDto? Caso);

public sealed record AnalysisSummaryDto(Guid Id, string FileName, DateTime CreatedAt, string Status,
    int TotalRequerimientos, int Aprobados, string? Proyecto);

public sealed record AnalysisDetailDto(Guid Id, string FileName, DateTime CreatedAt, string Status, string? Error,
    string? Resumen, IReadOnlyList<RequirementDetailDto> Requerimientos, string? Proyecto);

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
            a.Requirements.Count(r => r.Evaluation?.Passed == true), a.ProjectName)).ToArray();
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
            analysis.Requirements.Select(MapRequirement).ToArray(), analysis.ProjectName);
    }

    public static RequirementDetailDto MapRequirement(Requirement r) => new(
        r.Code, r.Text, r.Area,
        r.Evaluation is null ? null : new EvaluationDto(
            r.Code,
            r.Evaluation.Scores.Select(s => new CriterionDto(s.Criterion, s.Score, s.Observation)).ToArray(),
            Math.Round(r.Evaluation.Average, 2), r.Evaluation.Threshold, r.Evaluation.Passed),
        r.Clarifications.Select(c => new ClarificationDto(c.Question, c.Answer)).ToArray(),
        r.ReadyForStories,
        r.UseCase is null ? null : new UseCaseDto(
            r.UseCase.Nombre, r.UseCase.Objetivo, r.UseCase.Descripcion,
            r.UseCase.Actores.Select(a => new ActorDto(a.Nombre, a.Descripcion)).ToArray(),
            r.UseCase.Precondiciones,
            r.UseCase.Trigger,
            r.UseCase.Flujos.Select(f => new FlujoDto(
                f.Titulo, f.Pasos.Select(p => new PasoFlujoDto(p.Numero, p.Accion, p.ResultadoEsperado)).ToArray()))
                .ToArray(),
            r.UseCase.Extensiones,
            r.UseCase.Frecuencia,
            r.UseCase.Importancia,
            r.UseCase.Urgencia,
            r.UseCase.Comentarios));
}
