using MongoDB.Bson.Serialization.Attributes;
using RequirementsCopilot.Application.Conversations;

namespace RequirementsCopilot.Infrastructure.Mongo;

[BsonIgnoreExtraElements]
public sealed class ConversationDocument
{
    [BsonId]
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? AnalysisId { get; set; }
    public string? LastResponseId { get; set; }
    public string? ProjectName { get; set; }
    public List<ConversationMessageDocument> Messages { get; set; } = new();

    public static ConversationDocument FromRecord(ConversationRecord record) => new()
    {
        Id = record.Id,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt,
        Status = record.Status,
        AnalysisId = record.AnalysisId,
        LastResponseId = record.LastResponseId,
        ProjectName = record.ProjectName,
        Messages = record.Messages.Select(m => new ConversationMessageDocument { Role = m.Role, Text = m.Text }).ToList(),
    };

    public ConversationRecord ToRecord() => ConversationRecord.Rehydrate(
        Id, CreatedAt, UpdatedAt, Status, AnalysisId, LastResponseId,
        Messages.Select(m => new ConversationMessage(m.Role, m.Text)), ProjectName);
}

[BsonIgnoreExtraElements]
public sealed class ConversationMessageDocument
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
