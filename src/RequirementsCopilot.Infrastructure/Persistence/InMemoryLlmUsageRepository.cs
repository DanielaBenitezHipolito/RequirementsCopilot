using System.Collections.Concurrent;
using RequirementsCopilot.Application.Usage;

namespace RequirementsCopilot.Infrastructure.Persistence;

public sealed class InMemoryLlmUsageRepository : ILlmUsageRepository
{
    private readonly ConcurrentQueue<LlmUsage> _store = new();

    public Task AddAsync(LlmUsage usage, CancellationToken cancellationToken = default)
    {
        _store.Enqueue(usage);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LlmUsage>> GetSinceAsync(DateTime since, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LlmUsage>>(
            _store.Where(u => u.Timestamp >= since).OrderBy(u => u.Timestamp).ToArray());
}
