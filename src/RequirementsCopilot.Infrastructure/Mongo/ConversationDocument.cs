using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RequirementsCopilot.Application.Conversations;

namespace RequirementsCopilot.Infrastructure.Mongo;

public sealed class ConversationDocument
{
    // MongoDB.Driver 3.x no serializa Guid sin representación explícita (mismo fix que AnalysisDocument).
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? AnalysisId { get; set; }
    public string? LastResponseId { get; set; }
    public List<ConversationMessageDocument> Messages { get; set; } = new();

    public static ConversationDocument FromRecord(ConversationRecord record) => new()
    {
        Id = record.Id,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt,
        Status = record.Status,
        AnalysisId = record.AnalysisId,
        LastResponseId = record.LastResponseId,
        Messages = record.Messages.Select(m => new ConversationMessageDocument { Role = m.Role, Text = m.Text }).ToList(),
    };

    public ConversationRecord ToRecord() => ConversationRecord.Rehydrate(
        Id, CreatedAt, UpdatedAt, Status, AnalysisId, LastResponseId,
        Messages.Select(m => new ConversationMessage(m.Role, m.Text)));
}

public sealed class ConversationMessageDocument
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
