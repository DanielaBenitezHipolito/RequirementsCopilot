using System.Text;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Application.Projects;
using RequirementsCopilot.Infrastructure.Persistence;
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

    private const string UseCaseReply =
        "{\"nombre\":\"Módulo de Pólizas – Sistema HC Consulting\",\"objetivo\":\"Registrar un pago\"," +
        "\"descripcion\":\"El cajero registra el pago de una reserva.\"," +
        "\"actores\":[{\"nombre\":\"Cajero\",\"descripcion\":\"Registra el pago\"}]," +
        "\"precondiciones\":[\"Caja abierta\"],\"trigger\":\"El huésped paga\"," +
        "\"flujos\":[{\"titulo\":\"Proceso de creación manual\",\"pasos\":[" +
        "{\"numero\":1,\"accion\":\"Registrar\",\"resultadoEsperado\":\"Registrado\"}]}]," +
        "\"extensiones\":[],\"frecuencia\":\"Única\",\"importancia\":\"Alta\",\"urgencia\":\"Alta\",\"comentarios\":[]}";

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
            UseCaseWriterAgent.AgentName => UseCaseReply,
            UserStoryWriterAgent.AgentName =>
                "{\"historias\":[{\"titulo\":\"Registrar pago\",\"como\":\"Cajero\",\"quiero\":\"registrar el pago\"," +
                "\"para\":\"trazabilidad\",\"criteriosAceptacion\":[\"Consecutivo único\"],\"puntos\":3,\"dependencias\":[]}]}",
            ExecutiveSummaryAgent.AgentName => "{\"resumen\":\"Resumen ejecutivo de prueba.\"}",
            _ => "{}",
        },
    };

    private static AnalysisOrchestrator Orchestrator(StubChatCompletion chat, StubRepository repository) => new(
        new StubExtractor(),
        new RequirementExtractorAgent(chat),
        new RequirementEvaluatorAgent(chat),
        new ClarifierAgent(chat),
        new UseCaseWriterAgent(chat),
        new UserStoryWriterAgent(chat),
        new ExecutiveSummaryAgent(chat),
        repository,
        new AnalysisOptions(),
        Projects());

    private static ProjectContextLoader Projects(params Project[] projects)
    {
        var store = new InMemoryProjectRepository();
        foreach (Project project in projects)
        {
            store.SaveAsync(project).Wait();
        }
        return new ProjectContextLoader(store, new AnalysisOptions());
    }

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
    public async Task AnalyzeAsync_EvaluaYPregunta_SinCasoDeUsoAutomatico()
    {
        var repository = new StubRepository();
        var events = await Collect(Orchestrator(PipelineChat(), repository));

        Assert.Equal(
            new[]
            {
                AnalysisEventKind.Status, AnalysisEventKind.Requirement, AnalysisEventKind.Evaluation,
                AnalysisEventKind.Requirement, AnalysisEventKind.Evaluation, AnalysisEventKind.Clarification,
                AnalysisEventKind.Summary, AnalysisEventKind.Done,
            },
            events.Select(e => e.Kind).ToArray());

        Assert.Equal(AnalysisStatus.Completed, repository.Saved!.Status);
        Assert.Equal(2, repository.Saved.Requirements.Count);
        Assert.All(repository.Saved.Requirements, r => Assert.Null(r.UseCase)); // nunca automático
        Assert.Empty(repository.Saved.Requirements[0].Clarifications); // aprobado y claro: sin preguntas
        Assert.Equal(2, repository.Saved.Requirements[1].Clarifications.Count); // ambiguo: preguntas
        Assert.False(events[4].Evaluation!.Pasa);
        Assert.Equal("REQ-002", events[5].Clarification!.RequirementCode);
        Assert.Equal("Resumen ejecutivo de prueba.", repository.Saved.Summary);
    }

    [Fact]
    public async Task GenerateStoriesAsync_RequerimientoAprobado_GeneraCasoDeUso()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(PipelineChat(), repository);
        await Collect(orchestrator);

        var dto = await orchestrator.GenerateStoriesAsync(repository.Saved!.Id, "REQ-001");

        Assert.NotNull(dto.Caso);
        Assert.Equal("Módulo de Pólizas – Sistema HC Consulting", dto.Caso!.Nombre);
        Assert.NotNull(repository.Saved.Requirements[0].UseCase); // persistido
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
        Assert.NotNull(dto.Caso);
    }

    [Fact]
    public async Task GenerateStoriesAsync_AnalisisInexistente_LanzaNotFound()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(PipelineChat(), repository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => orchestrator.GenerateStoriesAsync(Guid.NewGuid(), "REQ-001"));
    }

    /// <summary>Chat cuyo evaluador aprueba cuando el input trae aclaraciones respondidas (mismo contrato del Fake real).</summary>
    private static StubChatCompletion ReevaluateChat()
    {
        var chat = PipelineChat();
        var baseReply = chat.Reply;
        chat.Reply = prompt => prompt.Agent == RequirementEvaluatorAgent.AgentName && prompt.Input.Contains("Aclaraciones respondidas")
            ? HighRubric()
            : baseReply(prompt);
        return chat;
    }

    [Fact]
    public async Task ReevaluateAsync_ResponderAclaraciones_Aprueba()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(ReevaluateChat(), repository);
        await Collect(orchestrator);

        var dto = await orchestrator.ReevaluateAsync(repository.Saved!.Id, "REQ-002",
            new[] { "Menos de 2 segundos", "Consultas y pagos" });

        Assert.True(dto.Evaluacion!.Pasa);
        Assert.Equal("Menos de 2 segundos", dto.Aclaraciones[0].Respuesta);
        Assert.True(repository.Saved.Requirements[1].Evaluation!.Passed); // persistido
    }

    [Fact]
    public async Task ReevaluateAsync_SiguenAmbiguo_AgregaPreguntasNuevasConservandoRespondidas()
    {
        var repository = new StubRepository();
        // El evaluador nunca aprueba, incluso con aclaraciones: sigue ambiguo tras responder.
        var chat = PipelineChat();
        var baseReply = chat.Reply;
        chat.Reply = prompt => prompt.Agent == RequirementEvaluatorAgent.AgentName ? LowRubric() : baseReply(prompt);
        var orchestrator = Orchestrator(chat, repository);
        await Collect(orchestrator);

        var dto = await orchestrator.ReevaluateAsync(repository.Saved!.Id, "REQ-002",
            new[] { "Menos de 2 segundos", "Consultas y pagos" });

        Assert.False(dto.Evaluacion!.Pasa);
        var requirement = repository.Saved.Requirements[1];
        Assert.Equal(4, requirement.Clarifications.Count); // 2 viejas respondidas + 2 nuevas
        Assert.True(requirement.Clarifications[0].IsAnswered);
        Assert.True(requirement.Clarifications[1].IsAnswered);
        Assert.False(requirement.Clarifications[2].IsAnswered);
    }

    [Fact]
    public async Task ReevaluateAsync_SinPreguntasPendientes_Lanza()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(ReevaluateChat(), repository);
        await Collect(orchestrator);

        // REQ-001 está aprobado, sin preguntas de clarificación.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.ReevaluateAsync(repository.Saved!.Id, "REQ-001", new[] { "algo" }));
    }

    [Fact]
    public async Task ReevaluateAsync_SinRespuestas_LanzaArgumentException()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(ReevaluateChat(), repository);
        await Collect(orchestrator);

        await Assert.ThrowsAsync<ArgumentException>(
            () => orchestrator.ReevaluateAsync(repository.Saved!.Id, "REQ-002", Array.Empty<string?>()));
    }

    [Fact]
    public async Task ReevaluateAsync_AnalisisInexistente_LanzaNotFound()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(ReevaluateChat(), repository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => orchestrator.ReevaluateAsync(Guid.NewGuid(), "REQ-001", new[] { "algo" }));
    }

    [Fact]
    public async Task ReevaluateAsync_EvaluadorFalla_LanzaLlmException()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(PipelineChat(), repository);
        await Collect(orchestrator);

        var chat = new StubChatCompletion { Reply = _ => "no json" };
        var brokenOrchestrator = new AnalysisOrchestrator(
            new StubExtractor(), new RequirementExtractorAgent(chat), new RequirementEvaluatorAgent(chat),
            new ClarifierAgent(chat), new UseCaseWriterAgent(chat), new UserStoryWriterAgent(chat),
            new ExecutiveSummaryAgent(chat), repository, new AnalysisOptions(), Projects());

        await Assert.ThrowsAsync<LlmException>(
            () => brokenOrchestrator.ReevaluateAsync(repository.Saved!.Id, "REQ-002", new[] { "algo", "algo" }));
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
        Assert.Null(requirement.UseCase); // el caso de uso sigue siendo manual
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

    private sealed class LongTextExtractor : IDocumentTextExtractor
    {
        public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
            => Task.FromResult(new string('x', 500));
    }

    [Fact]
    public async Task AnalyzeAsync_DocumentoLargo_TruncaAntesDelExtractor()
    {
        var chat = PipelineChat();
        var orchestrator = new AnalysisOrchestrator(
            new LongTextExtractor(),
            new RequirementExtractorAgent(chat),
            new RequirementEvaluatorAgent(chat),
            new ClarifierAgent(chat),
            new UseCaseWriterAgent(chat),
            new UserStoryWriterAgent(chat),
            new ExecutiveSummaryAgent(chat),
            new StubRepository(),
            new AnalysisOptions { MaxInputChars = 100 },
            Projects());

        await Collect(orchestrator);

        string extractorInput = chat.Prompts[0].Input;
        Assert.Contains(new string('x', 100), extractorInput);
        Assert.DoesNotContain(new string('x', 101), extractorInput);
    }
    [Fact]
    public async Task AnalyzeAsync_ConProyecto_InyectaContextoAEvaluadorYClarificadorYPersisteNombre()
    {
        var chat = PipelineChat();
        var repository = new StubRepository();
        var orchestrator = new AnalysisOrchestrator(
            new StubExtractor(), new RequirementExtractorAgent(chat), new RequirementEvaluatorAgent(chat),
            new ClarifierAgent(chat), new UseCaseWriterAgent(chat), new UserStoryWriterAgent(chat),
            new ExecutiveSummaryAgent(chat),
            repository, new AnalysisOptions(),
            Projects(Project.Create("Hotelería", "# Sistema de reservas\nYa existe módulo de pagos.")));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("doc"));
        await foreach (var _ in orchestrator.AnalyzeAsync(stream, "spec.txt", "Hotelería")) { }

        Assert.Equal("Hotelería", repository.Saved!.ProjectName);
        var evaluator = chat.Prompts.First(p => p.Agent == RequirementEvaluatorAgent.AgentName);
        var clarifier = chat.Prompts.First(p => p.Agent == ClarifierAgent.AgentName);
        Assert.StartsWith("CONTEXTO DEL PROYECTO EXISTENTE «Hotelería»", evaluator.Input);
        Assert.Contains("Ya existe módulo de pagos.", clarifier.Input);
        Assert.DoesNotContain("CONTEXTO DEL PROYECTO", chat.Prompts.First(p => p.Agent == RequirementExtractorAgent.AgentName).Input);
    }

    [Fact]
    public async Task AnalyzeAsync_SinProyecto_NoInyectaContexto()
    {
        var chat = PipelineChat();
        await Collect(Orchestrator(chat, new StubRepository()));

        Assert.All(chat.Prompts, p => Assert.DoesNotContain("CONTEXTO DEL PROYECTO", p.Input));
    }

    [Fact]
    public async Task CreateFromRequirementAsync_ConProyecto_ContextoLlegaAEvaluadorYAlRedactorDeCasos()
    {
        var chat = PipelineChat();
        var repository = new StubRepository();
        var orchestrator = new AnalysisOrchestrator(
            new StubExtractor(), new RequirementExtractorAgent(chat), new RequirementEvaluatorAgent(chat),
            new ClarifierAgent(chat), new UseCaseWriterAgent(chat), new UserStoryWriterAgent(chat),
            new ExecutiveSummaryAgent(chat),
            repository, new AnalysisOptions(),
            Projects(Project.Create("Hotelería", "Descripción del sistema.")));

        Guid id = await orchestrator.CreateFromRequirementAsync("REQ-001 El sistema debe registrar pagos", "Pagos", "Hotelería");
        await orchestrator.GenerateStoriesAsync(id, "REQ-001");

        Assert.Equal("Hotelería", repository.Saved!.ProjectName);
        Assert.Contains("Descripción del sistema.", chat.Prompts.First(p => p.Agent == RequirementEvaluatorAgent.AgentName).Input);
        Assert.Contains("Descripción del sistema.", chat.Prompts.First(p => p.Agent == UseCaseWriterAgent.AgentName).Input);
    }

    [Fact]
    public async Task CreateFromRequirementAsync_ProyectoInexistente_SeTrataComoNuevo()
    {
        var chat = PipelineChat();
        var repository = new StubRepository();
        await Orchestrator(chat, repository).CreateFromRequirementAsync("REQ-001 texto", "Pagos", "NoExiste");

        Assert.Equal("NoExiste", repository.Saved!.ProjectName);
        Assert.All(chat.Prompts, p => Assert.DoesNotContain("CONTEXTO DEL PROYECTO", p.Input));
    }
    [Fact]
    public async Task GenerateUserStoriesAsync_RequerimientoListo_GeneraConContexto()
    {
        var chat = PipelineChat();
        var repository = new StubRepository();
        var orchestrator = new AnalysisOrchestrator(
            new StubExtractor(), new RequirementExtractorAgent(chat), new RequirementEvaluatorAgent(chat),
            new ClarifierAgent(chat), new UseCaseWriterAgent(chat), new UserStoryWriterAgent(chat),
            new ExecutiveSummaryAgent(chat),
            repository, new AnalysisOptions(),
            Projects(Project.Create("Hotelería", "Ya existe check-in.")));

        Guid id = await orchestrator.CreateFromRequirementAsync("REQ-001 registrar pagos", "Pagos", "Hotelería");
        var dto = await orchestrator.GenerateUserStoriesAsync(id, "REQ-001");

        var historia = Assert.Single(dto.Historias);
        Assert.Equal("Registrar pago", historia.Titulo);
        Assert.Equal(3, historia.Puntos);
        Assert.Contains("Ya existe check-in.",
            chat.Prompts.First(p => p.Agent == UserStoryWriterAgent.AgentName).Input);
        Assert.Single(repository.Saved!.Requirements[0].UserStories);
    }

    [Fact]
    public async Task GenerateUserStoriesAsync_YaGeneradas_Rechaza()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(PipelineChat(), repository);
        Guid id = await orchestrator.CreateFromRequirementAsync("REQ-001 registrar pagos", "Pagos");
        await orchestrator.GenerateUserStoriesAsync(id, "REQ-001");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.GenerateUserStoriesAsync(id, "REQ-001"));
    }

    [Fact]
    public async Task GenerateUserStoriesAsync_NoListo_Rechaza()
    {
        var repository = new StubRepository();
        var orchestrator = Orchestrator(PipelineChat(), repository);
        await Collect(orchestrator);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.GenerateUserStoriesAsync(repository.Saved!.Id, "REQ-002"));
    }

    [Fact]
    public async Task GenerateUserStoriesAsync_AgenteSinHistorias_LanzaLlmException()
    {
        var chat = PipelineChat();
        var repository = new StubRepository();
        var orchestrator = Orchestrator(chat, repository);
        Guid id = await orchestrator.CreateFromRequirementAsync("REQ-001 registrar pagos", "Pagos");

        chat.Reply = _ => "{\"historias\":[]}";
        await Assert.ThrowsAsync<LlmException>(() => orchestrator.GenerateUserStoriesAsync(id, "REQ-001"));
    }
}
