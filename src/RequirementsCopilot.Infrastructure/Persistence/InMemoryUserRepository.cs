using System.Collections.Concurrent;
using RequirementsCopilot.Application.Users;

namespace RequirementsCopilot.Infrastructure.Persistence;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, UserAccount> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(UserAccount user, CancellationToken cancellationToken = default)
    {
        _store[user.Email] = user;
        return Task.CompletedTask;
    }

    public Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(email.Trim(), out UserAccount? user) ? user : null);

    public Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserAccount>>(_store.Values.OrderBy(u => u.Email).ToArray());

    public Task<bool> DeleteAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryRemove(email.Trim(), out _));
}
