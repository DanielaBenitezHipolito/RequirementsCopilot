using System.Collections.Concurrent;
using RequirementsCopilot.Application.Conversations;

namespace RequirementsCopilot.Infrastructure.Persistence;

public sealed class InMemoryConversationRepository : IConversationRepository
{
    private readonly ConcurrentDictionary<Guid, ConversationRecord> _store = new();

    public Task SaveAsync(ConversationRecord record, CancellationToken cancellationToken = default)
    {
        _store[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<ConversationRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id, out ConversationRecord? record) ? record : null);

    public Task<IReadOnlyList<ConversationRecord>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ConversationRecord>>(_store.Values.OrderByDescending(c => c.UpdatedAt).ToArray());
}
