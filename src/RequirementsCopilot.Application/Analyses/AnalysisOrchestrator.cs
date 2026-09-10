using System.Runtime.CompilerServices;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Application.Projects;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public sealed class AnalysisOrchestrator
{
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly RequirementExtractorAgent _extractor;
    private readonly RequirementEvaluatorAgent _evaluator;
    private readonly ClarifierAgent _clarifier;
    private readonly UseCaseWriterAgent _useCaseWriter;
    private readonly UserStoryWriterAgent _userStoryWriter;
    private readonly ExecutiveSummaryAgent _summaryAgent;
    private readonly IAnalysisRepository _repository;
    private readonly AnalysisOptions _options;
    private readonly ProjectContextLoader _projectContext;

    public AnalysisOrchestrator(IDocumentTextExtractor textExtractor, RequirementExtractorAgent extractor,
        RequirementEvaluatorAgent evaluator, ClarifierAgent clarifier, UseCaseWriterAgent useCaseWriter,
        UserStoryWriterAgent userStoryWriter, ExecutiveSummaryAgent summaryAgent, IAnalysisRepository repository,
        AnalysisOptions options, ProjectContextLoader projectContext)
    {
        _userStoryWriter = userStoryWriter;
        _textExtractor = textExtractor;
        _extractor = extractor;
        _evaluator = evaluator;
        _clarifier = clarifier;
        _useCaseWriter = useCaseWriter;
        _summaryAgent = summaryAgent;
        _repository = repository;
        _options = options;
        _projectContext = projectContext;
    }

    /// <summary>
    /// Pipeline del upload: extraer → evaluar → preguntas de clarificación si hay ambigüedad.
    /// El caso de uso NO se genera aquí: se dispara manualmente con <see cref="GenerateStoriesAsync"/>.
    /// </summary>
    public async IAsyncEnumerable<AnalysisEvent> AnalyzeAsync(Stream content, string fileName,
        string? projectName = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Analysis analysis = Analysis.Create(fileName, projectName);
        yield return AnalysisEvent.Status("Extrayendo requerimientos del documento…");
        string projectContext = await _projectContext.LoadAsync(projectName, cancellationToken);

        string? error = null;
        IReadOnlyList<Requirement> requirements = Array.Empty<Requirement>();
        try
        {
            string text = await _textExtractor.ExtractAsync(content, fileName, cancellationToken);
            if (text.Length > _options.MaxInputChars)
            {
                // Control de costos: documentos enormes se truncan antes de ir al LLM.
                text = text[.._options.MaxInputChars];
            }
            requirements = await _extractor.ExtractAsync(text, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { error = ex.Message; }

        if (error is null)
        {
            foreach (Requirement requirement in requirements)
            {
                analysis.AddRequirement(requirement);
                yield return AnalysisEvent.FromRequirement(requirement);

                Evaluation? evaluation = null;
                try
                {
                    evaluation = await _evaluator.EvaluateAsync(requirement, _options.PassThreshold,
                        answeredClarifications: null, projectContext, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { error = ex.Message; }
                if (error is not null)
                {
                    break;
                }

                requirement.Evaluate(evaluation!);
                yield return AnalysisEvent.FromEvaluation(requirement);
                if (!NeedsClarification(evaluation!))
                {
                    continue;
                }

                IReadOnlyList<string> questions = Array.Empty<string>();
                try
                {
                    questions = await _clarifier.AskAsync(requirement, projectContext, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { error = ex.Message; }
                if (error is not null)
                {
                    break;
                }

                foreach (string question in questions)
                {
                    requirement.AddClarification(Clarification.Create(question));
                }
                if (requirement.Clarifications.Count > 0)
                {
                    yield return AnalysisEvent.FromClarifications(requirement);
                }
            }
        }

        AnalysisEvent? summaryEvent = null;
        if (error is null && analysis.Requirements.Count > 0)
        {
            try
            {
                string summary = await _summaryAgent.SummarizeAsync(analysis, cancellationToken);
                analysis.SetSummary(summary);
                summaryEvent = AnalysisEvent.FromSummary(summary);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // El resumen es un plus editorial: si falla, el análisis sigue siendo válido sin él.
            }
        }

        if (error is null)
        {
            analysis.Complete();
        }
        else
        {
            analysis.Fail(error);
        }

        // La persistencia también puede fallar (p. ej. Mongo sin permisos de escritura):
        // el stream debe terminar con un evento error, nunca cortarse sin avisar.
        try
        {
            await _repository.SaveAsync(analysis, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            error = $"No se pudo guardar el análisis: {ex.Message}";
        }

        if (error is null && summaryEvent is not null)
        {
            yield return summaryEvent;
        }
        yield return error is null ? AnalysisEvent.Done(analysis.Id) : AnalysisEvent.Error(error);
    }

    /// <summary>
    /// Entrada conversacional (v2): crea un análisis con el requerimiento armado en el chat,
    /// lo evalúa y genera preguntas de clarificación si aplica. Devuelve el id del análisis;
    /// desde ahí el flujo continúa igual que el de documentos (responder → generar caso de uso).
    /// </summary>
    public async Task<Guid> CreateFromRequirementAsync(string text, string? area,
        string? projectName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("El requerimiento no puede estar vacío.");
        }

        string title = text.Trim();
        Analysis analysis = Analysis.Create(
            $"Conversación: {(title.Length > 40 ? title[..40] + "…" : title)}", projectName);
        Requirement requirement = Requirement.Create("REQ-001", text, area ?? "General");
        analysis.AddRequirement(requirement);

        try
        {
            string projectContext = await _projectContext.LoadAsync(projectName, cancellationToken);
            Evaluation evaluation = await _evaluator.EvaluateAsync(requirement, _options.PassThreshold,
                answeredClarifications: null, projectContext, cancellationToken);
            requirement.Evaluate(evaluation);
            if (NeedsClarification(evaluation))
            {
                foreach (string question in await _clarifier.AskAsync(requirement, projectContext, cancellationToken))
                {
                    requirement.AddClarification(Clarification.Create(question));
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            analysis.Fail(ex.Message);
            await _repository.SaveAsync(analysis, cancellationToken);
            throw new InvalidOperationException(ex.Message);
        }

        analysis.Complete();
        await _repository.SaveAsync(analysis, cancellationToken);
        return analysis.Id;
    }

    /// <summary>Guarda las respuestas del cliente a las preguntas de clarificación (por índice).</summary>
    public async Task<RequirementDetailDto> AnswerClarificationsAsync(Guid analysisId, string requirementCode,
        IReadOnlyList<string?> answers, CancellationToken cancellationToken = default)
    {
        (Analysis analysis, Requirement requirement) = await FindAsync(analysisId, requirementCode, cancellationToken);
        if (requirement.Clarifications.Count == 0)
        {
            throw new InvalidOperationException("El requerimiento no tiene preguntas de clarificación.");
        }

        for (int index = 0; index < answers.Count && index < requirement.Clarifications.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(answers[index]))
            {
                requirement.Clarifications[index].Respond(answers[index]!);
            }
        }

        await _repository.SaveAsync(analysis, cancellationToken);
        return AnalysisQueries.MapRequirement(requirement);
    }

    /// <summary>
    /// Generación manual: caso de uso (formato plantilla corporativa) para un requerimiento listo
    /// (aprobado, o con todas sus clarificaciones respondidas).
    /// </summary>
    public async Task<RequirementDetailDto> GenerateStoriesAsync(Guid analysisId, string requirementCode,
        CancellationToken cancellationToken = default)
    {
        (Analysis analysis, Requirement requirement) = await FindAsync(analysisId, requirementCode, cancellationToken);
        if (requirement.UseCase is not null)
        {
            throw new InvalidOperationException("El requerimiento ya tiene un caso de uso generado.");
        }
        if (!requirement.ReadyForStories)
        {
            throw new InvalidOperationException(
                "Responda las preguntas de clarificación antes de generar el caso de uso.");
        }

        string projectContext = await _projectContext.LoadAsync(analysis.ProjectName, cancellationToken);
        requirement.SetUseCase(await _useCaseWriter.WriteAsync(requirement, projectContext, cancellationToken));

        await _repository.SaveAsync(analysis, cancellationToken);
        return AnalysisQueries.MapRequirement(requirement);
    }

    /// <summary>
    /// Generación manual (ADR-0006): backlog de historias de usuario con puntos Fibonacci para un
    /// requerimiento listo. Reutiliza aclaraciones, caso de uso y contexto del proyecto.
    /// </summary>
    public async Task<RequirementDetailDto> GenerateUserStoriesAsync(Guid analysisId, string requirementCode,
        CancellationToken cancellationToken = default)
    {
        (Analysis analysis, Requirement requirement) = await FindAsync(analysisId, requirementCode, cancellationToken);
        if (requirement.UserStories.Count > 0)
        {
            throw new InvalidOperationException("El requerimiento ya tiene historias de usuario generadas.");
        }
        if (!requirement.ReadyForStories)
        {
            throw new InvalidOperationException(
                "Responda las preguntas de clarificación antes de generar las historias.");
        }

        string projectContext = await _projectContext.LoadAsync(analysis.ProjectName, cancellationToken);
        IReadOnlyList<UserStory> stories;
        try
        {
            stories = await _userStoryWriter.WriteAsync(requirement, projectContext, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex) { throw new LlmException(ex.Message); }

        requirement.SetUserStories(stories);
        await _repository.SaveAsync(analysis, cancellationToken);
        return AnalysisQueries.MapRequirement(requirement);
    }

    /// <summary>
    /// Ciclo responder → re-evaluar → más preguntas: guarda las respuestas a las preguntas pendientes,
    /// re-evalúa el requerimiento con esas aclaraciones incorporadas y, si sigue ambiguo, agrega
    /// preguntas nuevas (las anteriores conservan sus respuestas). Se itera hasta aprobar.
    /// </summary>
    public async Task<RequirementDetailDto> ReevaluateAsync(Guid analysisId, string requirementCode,
        IReadOnlyList<string?> answers, CancellationToken cancellationToken = default)
    {
        (Analysis analysis, Requirement requirement) = await FindAsync(analysisId, requirementCode, cancellationToken);

        var pending = requirement.Clarifications.Where(c => !c.IsAnswered).ToArray();
        if (pending.Length == 0)
        {
            throw new InvalidOperationException("El requerimiento no tiene preguntas de clarificación pendientes.");
        }
        if (answers.Count == 0 || answers.All(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Debe responder al menos una pregunta pendiente.", nameof(answers));
        }

        for (int index = 0; index < answers.Count && index < pending.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(answers[index]))
            {
                pending[index].Respond(answers[index]!);
            }
        }

        string projectContext = await _projectContext.LoadAsync(analysis.ProjectName, cancellationToken);
        Evaluation evaluation;
        try
        {
            evaluation = await _evaluator.EvaluateAsync(requirement, _options.PassThreshold,
                requirement.Clarifications, projectContext, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new LlmException(ex.Message); }

        requirement.Evaluate(evaluation);

        if (NeedsClarification(evaluation))
        {
            IReadOnlyList<string> questions;
            try
            {
                questions = await _clarifier.AskAsync(requirement, projectContext, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new LlmException(ex.Message); }

            foreach (string question in questions)
            {
                requirement.AddClarification(Clarification.Create(question));
            }
        }

        await _repository.SaveAsync(analysis, cancellationToken);
        return AnalysisQueries.MapRequirement(requirement);
    }

    private async Task<(Analysis, Requirement)> FindAsync(Guid analysisId, string requirementCode,
        CancellationToken cancellationToken)
    {
        Analysis analysis = await _repository.GetByIdAsync(analysisId, cancellationToken)
            ?? throw new KeyNotFoundException("Análisis no encontrado.");
        Requirement requirement = analysis.Requirements
            .FirstOrDefault(r => string.Equals(r.Code, requirementCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("Requerimiento no encontrado en el análisis.");
        return (analysis, requirement);
    }

    private static bool NeedsClarification(Evaluation evaluation) =>
        !evaluation.Passed ||
        evaluation.Scores.Any(s => s.Criterion is "Claridad" or "Completitud" && s.Score < 4);
}
