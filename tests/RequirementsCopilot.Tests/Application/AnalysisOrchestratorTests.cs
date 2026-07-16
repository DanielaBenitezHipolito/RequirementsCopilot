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
        public Task<Analysis?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Saved?.Id == id ? Saved : null);
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
            ClarifierAgent.AgentName =>
                "{\"preguntas\":[\"¿Qué significa rápido en segundos?\",\"¿Para qué operaciones aplica?\"]}",
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
        new ClarifierAgent(chat),
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
    public async Task AnalyzeAsync_EvaluaYPregunta_SinHistoriasAutomaticas()
    {
        var repository = new StubRepository();
        var events = await Collect(Orchestrator(PipelineChat(), repository));

        Assert.Equal(
            new[]
            {
                AnalysisEventKind.Status, AnalysisEventKind.Requirement, AnalysisEventKind.Evaluation,
                AnalysisEventKind.Requirement, AnalysisEventKind.Evaluation, AnalysisEventKind.Clarification,
                AnalysisEventKind.Done,
            },
            events.Select(e => e.Kind).ToArray());

        Assert.Equal(AnalysisStatus.Completed, repository.Saved!.Status);
        Assert.Equal(2, repository.Saved.Requirements.Count);
        Assert.All(repository.Saved.Requirements, r => Assert.Empty(r.Stories)); // nunca automáticas
        Assert.Empty(repository.Saved.Requirements[0].Clarifications); // aprobado y claro: sin preguntas
        Assert.Equal(2, repository.Saved.Requirements[1].Clarifications.Count); // ambiguo: preguntas
        Assert.False(events[4].Evaluation!.Pasa);
        Assert.Equal("REQ-002", events[5].Clarification!.RequirementCode);
    }

    [Fact]
    public async Task GenerateStoriesAsync_RequerimientoAprobado_GeneraHistoriasConCaso()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(PipelineChat(), repository);
        await Collect(orchestrator);

        var dto = await orchestrator.GenerateStoriesAsync(repository.Saved!.Id, "REQ-001");

        Assert.Single(dto.Historias);
        Assert.NotNull(dto.Historias[0].Caso);
        Assert.Single(repository.Saved.Requirements[0].Stories); // persistido
    }

    [Fact]
    public async Task GenerateStoriesAsync_SinResponderClarificaciones_Rechaza()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(PipelineChat(), repository);
        await Collect(orchestrator);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.GenerateStoriesAsync(repository.Saved!.Id, "REQ-002"));
    }

    [Fact]
    public async Task GenerateStoriesAsync_ClarificacionesRespondidas_Genera()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(PipelineChat(), repository);
        await Collect(orchestrator);

        var answered = await orchestrator.AnswerClarificationsAsync(repository.Saved!.Id, "REQ-002",
            new[] { "Menos de 2 segundos", "Consultas y pagos" });
        Assert.True(answered.ListoParaHistorias);

        var dto = await orchestrator.GenerateStoriesAsync(repository.Saved.Id, "REQ-002");
        Assert.Single(dto.Historias);
    }

    [Fact]
    public async Task GenerateStoriesAsync_AnalisisInexistente_LanzaNotFound()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(PipelineChat(), repository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => orchestrator.GenerateStoriesAsync(Guid.NewGuid(), "REQ-001"));
    }

    [Fact]
    public async Task CreateFromRequirementAsync_RequerimientoClaro_CreaAnalisisEvaluado()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(PipelineChat(), repository);

        // el stub evalúa alto cuando el input contiene REQ-001 (código asignado por la entrada conversacional)
        Guid id = await orchestrator.CreateFromRequirementAsync("El sistema debe registrar pagos con consecutivo", "Pagos");

        Assert.Equal(id, repository.Saved!.Id);
        Assert.Equal(AnalysisStatus.Completed, repository.Saved.Status);
        Assert.StartsWith("Conversación:", repository.Saved.FileName);
        var requirement = Assert.Single(repository.Saved.Requirements);
        Assert.True(requirement.Evaluation!.Passed);
        Assert.Empty(requirement.Stories); // las historias siguen siendo manuales
    }

    [Fact]
    public async Task CreateFromRequirementAsync_Ambiguo_GuardaPreguntasDeClarificacion()
    {
        var chat = PipelineChat();
        var baseReply = chat.Reply;
        chat.Reply = prompt => prompt.Agent == RequirementEvaluatorAgent.AgentName ? LowRubric() : baseReply(prompt);
        var repository = new StubRepository();

        await Orchestrator(chat, repository).CreateFromRequirementAsync("El sistema debe ser rápido", null);

        var requirement = Assert.Single(repository.Saved!.Requirements);
        Assert.False(requirement.Evaluation!.Passed);
        Assert.Equal(2, requirement.Clarifications.Count);
        Assert.Equal("General", requirement.Area);
    }

    [Fact]
    public async Task CreateFromRequirementAsync_EvaluadorFalla_PersisteFailedYLanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "no json" };
        var repository = new StubRepository();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Orchestrator(chat, repository).CreateFromRequirementAsync("texto válido", null));
        Assert.Equal(AnalysisStatus.Failed, repository.Saved!.Status);
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
