using RequirementsCopilot.Application.Conversations;
using RequirementsCopilot.Infrastructure.Mongo;

namespace RequirementsCopilot.Tests.Infrastructure;

public class ConversationDocumentTests
{
    [Fact]
    public void FromRecord_ToRecord_RoundTripAbierta()
    {
        ConversationRecord record = ConversationRecord.Create();
        record.Append("user", "Quiero controlar los pagos de las reservas");
        record.Append("agent", "¿Quién usará esta funcionalidad?");
        record.SetLastResponseId("resp-123");

        ConversationRecord restored = ConversationDocument.FromRecord(record).ToRecord();

        Assert.Equal(record.Id, restored.Id);
        Assert.Equal(record.CreatedAt, restored.CreatedAt);
        Assert.Equal(record.UpdatedAt, restored.UpdatedAt);
        Assert.Equal("Abierta", restored.Status);
        Assert.Null(restored.AnalysisId);
        Assert.Equal("resp-123", restored.LastResponseId);
        Assert.Equal(2, restored.Messages.Count);
        Assert.Equal("user", restored.Messages[0].Role);
        Assert.Equal("Quiero controlar los pagos de las reservas", restored.Messages[0].Text);
        Assert.Equal("agent", restored.Messages[1].Role);
    }

    [Fact]
    public void FromRecord_ToRecord_RoundTripCompletada()
    {
        ConversationRecord record = ConversationRecord.Create();
        record.Append("user", "idea");
        record.Append("agent", "listo");
        Guid analysisId = Guid.NewGuid();
        record.Complete(analysisId);

        ConversationRecord restored = ConversationDocument.FromRecord(record).ToRecord();

        Assert.Equal("Completada", restored.Status);
        Assert.Equal(analysisId, restored.AnalysisId);
    }
}
