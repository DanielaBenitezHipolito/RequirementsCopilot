using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using RequirementsCopilot.Application.Usage;

namespace RequirementsCopilot.Infrastructure.Mongo;

/// <summary>Documento Mongo de un consumo de LLM.</summary>
[BsonIgnoreExtraElements]
public sealed class LlmUsageDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Agent { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    public static LlmUsageDocument FromDomain(LlmUsage usage) => new()
    {
        Timestamp = usage.Timestamp,
        Agent = usage.Agent,
        Model = usage.Model,
        InputTokens = usage.InputTokens,
        OutputTokens = usage.OutputTokens,
    };

    public LlmUsage ToDomain() => new(Timestamp, Agent, Model, InputTokens, OutputTokens);
}

public sealed class MongoLlmUsageRepository : ILlmUsageRepository
{
    private readonly IMongoCollection<LlmUsageDocument> _collection;

    public MongoLlmUsageRepository(IMongoDatabase database)
        => _collection = database.GetCollection<LlmUsageDocument>("llm_usage");

    public Task AddAsync(LlmUsage usage, CancellationToken cancellationToken = default)
        => _collection.InsertOneAsync(LlmUsageDocument.FromDomain(usage), cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<LlmUsage>> GetSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        List<LlmUsageDocument> documents = await _collection.Find(d => d.Timestamp >= since)
            .SortBy(d => d.Timestamp).ToListAsync(cancellationToken);
        return documents.Select(d => d.ToDomain()).ToArray();
    }
}
