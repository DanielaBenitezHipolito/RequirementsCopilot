using MongoDB.Driver;
using RequirementsCopilot.Application.Conversations;

namespace RequirementsCopilot.Infrastructure.Mongo;

public sealed class MongoConversationRepository : IConversationRepository
{
    private readonly IMongoCollection<ConversationDocument> _collection;

    public MongoConversationRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ConversationDocument>("conversations");

    public Task SaveAsync(ConversationRecord record, CancellationToken cancellationToken = default)
        => _collection.ReplaceOneAsync(d => d.Id == record.Id, ConversationDocument.FromRecord(record),
            new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public async Task<ConversationRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ConversationDocument? document = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document?.ToRecord();
    }

    public async Task<IReadOnlyList<ConversationRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<ConversationDocument> documents = await _collection.Find(FilterDefinition<ConversationDocument>.Empty)
            .SortByDescending(d => d.UpdatedAt).ToListAsync(cancellationToken);
        return documents.Select(d => d.ToRecord()).ToArray();
    }
}
