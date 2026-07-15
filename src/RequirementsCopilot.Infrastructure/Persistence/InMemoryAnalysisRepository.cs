using System.Collections.Concurrent;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Infrastructure.Persistence;

public sealed class InMemoryAnalysisRepository : IAnalysisRepository
{
    private readonly ConcurrentDictionary<Guid, Analysis> _store = new();

    public Task SaveAsync(Analysis analysis, CancellationToken cancellationToken = default)
    {
        _store[analysis.Id] = analysis;
        return Task.CompletedTask;
    }

    public Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id, out Analysis? analysis) ? analysis : null);

    public Task<IReadOnlyList<Analysis>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Analysis>>(_store.Values.OrderByDescending(a => a.CreatedAt).ToArray());
}
