using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public interface IAnalysisRepository
{
    Task SaveAsync(Analysis analysis, CancellationToken cancellationToken = default);
    Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Analysis>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Análisis del proyecto indicado (comparación sin distinguir mayúsculas), ordenados por fecha
    /// ascendente. Alimenta el contexto acumulado de casos de uso del proyecto (ADR-0009).
    /// </summary>
    Task<IReadOnlyList<Analysis>> GetByProjectAsync(string projectName, CancellationToken cancellationToken = default);
}
