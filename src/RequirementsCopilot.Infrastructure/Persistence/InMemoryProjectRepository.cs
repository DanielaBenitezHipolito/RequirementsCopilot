using System.Collections.Concurrent;
using RequirementsCopilot.Application.Projects;

namespace RequirementsCopilot.Infrastructure.Persistence;

/// <summary>Adaptador en memoria del puerto <see cref="IProjectRepository"/> (desarrollo/pruebas).</summary>
public sealed class InMemoryProjectRepository : IProjectRepository
{
    private readonly ConcurrentDictionary<string, Project> _store = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task SaveAsync(Project project, CancellationToken cancellationToken = default)
    {
        _store[project.Name] = project;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Project?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(name.Trim(), out Project? project) ? project : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Project>>(
            _store.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray());

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryRemove(name.Trim(), out _));
}
