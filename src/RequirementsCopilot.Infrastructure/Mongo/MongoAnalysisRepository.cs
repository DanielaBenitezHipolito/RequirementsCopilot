using MongoDB.Driver;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Infrastructure.Mongo;

public sealed class MongoAnalysisRepository : IAnalysisRepository
{
    private readonly IMongoCollection<AnalysisDocument> _collection;

    public MongoAnalysisRepository(IMongoDatabase database)
        => _collection = database.GetCollection<AnalysisDocument>("analyses");

    public Task SaveAsync(Analysis analysis, CancellationToken cancellationToken = default)
        => _collection.ReplaceOneAsync(d => d.Id == analysis.Id, AnalysisDocument.FromDomain(analysis),
            new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public async Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AnalysisDocument? document = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<IReadOnlyList<Analysis>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<AnalysisDocument> documents = await _collection.Find(FilterDefinition<AnalysisDocument>.Empty)
            .SortByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
        return documents.Select(d => d.ToDomain()).ToArray();
    }
}
