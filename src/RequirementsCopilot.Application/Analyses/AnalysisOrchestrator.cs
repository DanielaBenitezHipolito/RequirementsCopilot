using System.Runtime.CompilerServices;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public sealed class AnalysisOrchestrator
{
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly RequirementExtractorAgent _extractor;
    private readonly RequirementEvaluatorAgent _evaluator;
    private readonly ClarifierAgent _clarifier;
    private readonly StoryWriterAgent _storyWriter;
    private readonly TestCaseWriterAgent _testCaseWriter;
    private readonly IAnalysisRepository _repository;
    private readonly AnalysisOptions _options;

    public AnalysisOrchestrator(IDocumentTextExtractor textExtractor, RequirementExtractorAgent extractor,
        RequirementEvaluatorAgent evaluator, ClarifierAgent clarifier, StoryWriterAgent storyWriter,
        TestCaseWriterAgent testCaseWriter, IAnalysisRepository repository, AnalysisOptions options)
    {
        _textExtractor = textExtractor;
        _extractor = extractor;
        _evaluator = evaluator;
        _clarifier = clarifier;
        _storyWriter = storyWriter;
        _testCaseWriter = testCaseWriter;
        _repository = repository;
        _options = options;
    }

    /// <summary>
    /// Pipeline del upload: extraer → evaluar → preguntas de clarificación si hay ambigüedad.
    /// Las historias NO se generan aquí: se disparan manualmente con <see cref="GenerateStoriesAsync"/>.
    /// </summary>
    public async IAsyncEnumerable<AnalysisEvent> AnalyzeAsync(Stream content, string fileName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Analysis analysis = Analysis.Create(fileName);
        yield return AnalysisEvent.Status("Extrayendo requerimientos del documento…");

        string? error = null;
        IReadOnlyList<Requirement> requirements = Array.Empty<Requirement>();
        try
        {
            string text = await _textExtractor.ExtractAsync(content, fileName, cancellationToken);
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
                    evaluation = await _evaluator.EvaluateAsync(requirement, _options.PassThreshold, cancellationToken);
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
                    questions = await _clarifier.AskAsync(requirement, cancellationToken);
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

        yield return error is null ? AnalysisEvent.Done(analysis.Id) : AnalysisEvent.Error(error);
    }

    /// <summary>
    /// Entrada conversacional (v2): crea un análisis con el requerimiento armado en el chat,
    /// lo evalúa y genera preguntas de clarificación si aplica. Devuelve el id del análisis;
    /// desde ahí el flujo continúa igual que el de documentos (responder → generar historias).
    /// </summary>
    public async Task<Guid> CreateFromRequirementAsync(string text, string? area,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("El requerimiento no puede estar vacío.");
        }

        string title = text.Trim();
        Analysis analysis = Analysis.Create(
            $"Conversación: {(title.Length > 40 ? title[..40] + "…" : title)}");
        Requirement requirement = Requirement.Create("REQ-001", text, area ?? "General");
        analysis.AddRequirement(requirement);

        try
        {
            Evaluation evaluation = await _evaluator.EvaluateAsync(requirement, _options.PassThreshold, cancellationToken);
            requirement.Evaluate(evaluation);
            if (NeedsClarification(evaluation))
            {
                foreach (string question in await _clarifier.AskAsync(requirement, cancellationToken))
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
    /// Generación manual: historias + caso de prueba por historia para un requerimiento listo
    /// (aprobado, o con todas sus clarificaciones respondidas).
    /// </summary>
    public async Task<RequirementDetailDto> GenerateStoriesAsync(Guid analysisId, string requirementCode,
        CancellationToken cancellationToken = default)
    {
        (Analysis analysis, Requirement requirement) = await FindAsync(analysisId, requirementCode, cancellationToken);
        if (requirement.Stories.Count > 0)
        {
            throw new InvalidOperationException("El requerimiento ya tiene historias generadas.");
        }
        if (!requirement.ReadyForStories)
        {
            throw new InvalidOperationException(
                "Responda las preguntas de clarificación antes de generar historias.");
        }

        IReadOnlyList<UserStory> stories = await _storyWriter.WriteAsync(requirement, cancellationToken);
        foreach (UserStory story in stories)
        {
            story.AttachTestCase(await _testCaseWriter.WriteAsync(story, cancellationToken));
            requirement.AddStory(story);
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
