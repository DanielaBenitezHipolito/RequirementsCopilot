using MongoDB.Driver;
using RequirementsCopilot.Application.Projects;

namespace RequirementsCopilot.Infrastructure.Mongo;

/// <summary>Adaptador Mongo del puerto <see cref="IProjectRepository"/> (colección <c>projects</c>).</summary>
public sealed class MongoProjectRepository : IProjectRepository
{
    private readonly IMongoCollection<ProjectDocument> _collection;

    /// <summary>Crea el repositorio sobre la base de datos dada.</summary>
    public MongoProjectRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ProjectDocument>("projects");

    /// <inheritdoc />
    public Task SaveAsync(Project project, CancellationToken cancellationToken = default)
    {
        string key = ProjectDocument.KeyFor(project.Name);
        return _collection.ReplaceOneAsync(d => d.Id == key, ProjectDocument.FromProject(project),
            new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Project?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        string key = ProjectDocument.KeyFor(name);
        ProjectDocument? document = await _collection.Find(d => d.Id == key).FirstOrDefaultAsync(cancellationToken);
        return document?.ToProject();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<ProjectDocument> documents = await _collection.Find(FilterDefinition<ProjectDocument>.Empty)
            .SortBy(d => d.Id).ToListAsync(cancellationToken);
        return documents.Select(d => d.ToProject()).ToArray();
    }
}
