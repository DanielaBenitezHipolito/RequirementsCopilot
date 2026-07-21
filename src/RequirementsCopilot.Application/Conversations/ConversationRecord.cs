namespace RequirementsCopilot.Application.Conversations;

/// <summary>
/// Historial persistido de una conversación del entrevistador conversacional (v2).
/// Se guarda desde el primer mensaje: permite listar, reabrir y continuar chats.
/// </summary>
public sealed class ConversationRecord
{
    public Guid Id { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string Status { get; private set; } = "Abierta";
    public Guid? AnalysisId { get; private set; }
    public string? LastResponseId { get; private set; }
    public List<ConversationMessage> Messages { get; private set; } = new();

    public static ConversationRecord Create()
    {
        DateTime now = DateTime.UtcNow;
        return new ConversationRecord { Id = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now, Status = "Abierta" };
    }

    /// <summary>Rehidrata desde persistencia (Mongo/InMemory), sin pasar por Create().</summary>
    public static ConversationRecord Rehydrate(Guid id, DateTime createdAt, DateTime updatedAt, string status,
        Guid? analysisId, string? lastResponseId, IEnumerable<ConversationMessage> messages) => new()
    {
        Id = id,
        CreatedAt = createdAt,
        UpdatedAt = updatedAt,
        Status = status,
        AnalysisId = analysisId,
        LastResponseId = lastResponseId,
        Messages = messages.ToList(),
    };

    public void Append(string role, string text)
    {
        Messages.Add(new ConversationMessage(role, text));
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetLastResponseId(string? responseId)
    {
        LastResponseId = responseId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(Guid analysisId)
    {
        AnalysisId = analysisId;
        Status = "Completada";
        UpdatedAt = DateTime.UtcNow;
    }
}
