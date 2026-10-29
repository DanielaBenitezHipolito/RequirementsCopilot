using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using RequirementsCopilot.Application.Users;

namespace RequirementsCopilot.Infrastructure.Mongo;

/// <summary>Documento Mongo de una cuenta de usuario (_id = email normalizado).</summary>
[BsonIgnoreExtraElements]
public sealed class UserDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public static UserDocument FromDomain(UserAccount user) => new()
    {
        Id = user.Email,
        Name = user.Name,
        Role = user.Role,
        PasswordHash = user.PasswordHash,
        CreatedAt = user.CreatedAt,
    };

    public UserAccount ToDomain() => new(Id, Name, Role, PasswordHash, CreatedAt);
}

public sealed class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<UserDocument> _collection;

    public MongoUserRepository(IMongoDatabase database)
        => _collection = database.GetCollection<UserDocument>("users");

    public Task SaveAsync(UserAccount user, CancellationToken cancellationToken = default)
        => _collection.ReplaceOneAsync(d => d.Id == user.Email, UserDocument.FromDomain(user),
            new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public async Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        string id = email.Trim().ToLowerInvariant();
        UserDocument? document = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<UserDocument> documents = await _collection.Find(FilterDefinition<UserDocument>.Empty)
            .SortBy(d => d.Id).ToListAsync(cancellationToken);
        return documents.Select(d => d.ToDomain()).ToArray();
    }

    public async Task<bool> DeleteAsync(string email, CancellationToken cancellationToken = default)
    {
        string id = email.Trim().ToLowerInvariant();
        DeleteResult result = await _collection.DeleteOneAsync(d => d.Id == id, cancellationToken);
        return result.DeletedCount > 0;
    }
}
