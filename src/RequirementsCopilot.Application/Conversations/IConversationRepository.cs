namespace RequirementsCopilot.Application.Conversations;

public interface IConversationRepository
{
    Task SaveAsync(ConversationRecord record, CancellationToken cancellationToken = default);
    Task<ConversationRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Ordenado por UpdatedAt descendente (más reciente primero).</summary>
    Task<IReadOnlyList<ConversationRecord>> GetAllAsync(CancellationToken cancellationToken = default);
}
