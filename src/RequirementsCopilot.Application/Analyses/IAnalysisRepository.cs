using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public interface IAnalysisRepository
{
    Task SaveAsync(Analysis analysis, CancellationToken cancellationToken = default);
    Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Analysis>> GetAllAsync(CancellationToken cancellationToken = default);
}
