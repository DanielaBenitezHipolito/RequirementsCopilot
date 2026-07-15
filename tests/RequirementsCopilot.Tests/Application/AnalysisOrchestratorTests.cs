using System.Text;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class AnalysisOrchestratorTests
{
    private sealed class StubRepository : IAnalysisRepository
    {
        public Analysis? Saved { get; private set; }
        public Task SaveAsync(Analysis analysis, CancellationToken ct = default) { Saved = analysis; return Task.CompletedTask; }
        public Task<Analysis?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Analysis?>(null);
        public Task<IReadOnlyList<Analysis>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Analysis>>(Array.Empty<Analysis>());
    }

    private sealed class StubExtractor : IDocumentTextExtractor
    {
        public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
            => Task.FromResult("texto del documento");
    }

    private static string HighRubric() =>
        "{\"criterios\":[{\"nombre\":\"Claridad\",\"score\":5,\"observacion\":\"ok\"},{\"nombre\":\"Completitud\",\"score\":5,\"observacion\":\"ok\"}," +
        "{\"nombre\":\"Verificabilidad\",\"score\":5,\"observacion\":\"ok\"},{\"nombre\":\"Consistencia\",\"score\":5,\"observacion\":\"ok\"}," +
        "{\"nombre\":\"Factibilidad\",\"score\":5,\"observacion\":\"ok\"}]}";

    private static string LowRubric() =>
        "{\"criterios\":[{\"nombre\":\"Claridad\",\"score\":1,\"observacion\":\"ambiguo\"},{\"nombre\":\"Completitud\",\"score\":2,\"observacion\":\"incompleto\"}," +
        "{\"nombre\":\"Verificabilidad\",\"score\":1,\"observacion\":\"no medible\"},{\"nombre\":\"Consistencia\",\"score\":2,\"observacion\":\"ok\"}," +
        "{\"nombre\":\"Factibilidad\",\"score\":2,\"observacion\":\"dudosa\"}]}";

    private static StubChatCompletion PipelineChat() => new()
    {
        Reply = prompt => prompt.Agent switch
        {
            RequirementExtractorAgent.AgentName =>
                "{\"requerimientos\":[{\"codigo\":\"REQ-001\",\"texto\":\"El sistema debe registrar pagos\",\"area\":\"Pagos\"}," +
                "{\"codigo\":\"REQ-002\",\"texto\":\"El sistema debe ser rápido\",\"area\":\"General\"}]}",
            RequirementEvaluatorAgent.AgentName => prompt.Input.Contains("REQ-001") ? HighRubric() : LowRubric(),
            StoryWriterAgent.AgentName =>
                "{\"historias\":[{\"rol\":\"cajero\",\"quiero\":\"registrar un pago\",\"para\":\"cerrar la venta\",\"criteriosAceptacion\":[\"dado A entonces B\"]}]}",
            TestCaseWriterAgent.AgentName =>
                "{\"titulo\":\"Pago exitoso\",\"precondiciones\":[\"caja abierta\"],\"pasos\":[\"registrar\"],\"resultadoEsperado\":\"registrado\"}",
            _ => "{}",
        },
    };

    private static AnalysisOrchestrator Orchestrator(StubChatCompletion chat, StubRepository repository) => new(
        new StubExtractor(),
        new RequirementExtractorAgent(chat),
        new RequirementEvaluatorAgent(chat),
        new StoryWriterAgent(chat),
        new TestCaseWriterAgent(chat),
        repository,
        new AnalysisOptions());

    private static async Task<List<AnalysisEvent>> Collect(AnalysisOrchestrator orchestrator)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("doc"));
        var events = new List<AnalysisEvent>();
        await foreach (var analysisEvent in orchestrator.AnalyzeAsync(stream, "spec.txt"))
        {
            events.Add(analysisEvent);
        }
        return events;
    }

    [Fact]
    public async Task AnalyzeAsync_PipelineCompleto_EmiteEventosEnOrdenYPersiste()
    {
        var repository = new StubRepository();
        var events = await Collect(Orchestrator(PipelineChat(), repository));

        Assert.Equal(
            new[]
            {
                AnalysisEventKind.Status, AnalysisEventKind.Requirement, AnalysisEventKind.Evaluation,
                AnalysisEventKind.Story, AnalysisEventKind.TestCase, AnalysisEventKind.Requirement,
                AnalysisEventKind.Evaluation, AnalysisEventKind.Done,
            },
            events.Select(e => e.Kind).ToArray());

        Assert.Equal(AnalysisStatus.Completed, repository.Saved!.Status);
        Assert.Equal(2, repository.Saved.Requirements.Count);
        Assert.Single(repository.Saved.Requirements[0].Stories);
        Assert.NotNull(repository.Saved.Requirements[0].Stories[0].TestCase);
        Assert.Empty(repository.Saved.Requirements[1].Stories); // no pasó: guardrail, sin historias
        Assert.False(events[6].Evaluation!.Pasa);
    }

    [Fact]
    public async Task AnalyzeAsync_ExtractorFalla_EmiteErrorYPersisteFailed()
    {
        var chat = new StubChatCompletion { Reply = _ => "no json" };
        var repository = new StubRepository();

        var events = await Collect(Orchestrator(chat, repository));

        Assert.Equal(AnalysisEventKind.Error, events[^1].Kind);
        Assert.Equal(AnalysisStatus.Failed, repository.Saved!.Status);
        Assert.NotNull(repository.Saved.Error);
    }
}
