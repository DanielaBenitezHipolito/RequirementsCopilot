using System.Runtime.CompilerServices;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public sealed class AnalysisOrchestrator
{
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly RequirementExtractorAgent _extractor;
    private readonly RequirementEvaluatorAgent _evaluator;
    private readonly StoryWriterAgent _storyWriter;
    private readonly TestCaseWriterAgent _testCaseWriter;
    private readonly IAnalysisRepository _repository;
    private readonly AnalysisOptions _options;

    public AnalysisOrchestrator(IDocumentTextExtractor textExtractor, RequirementExtractorAgent extractor,
        RequirementEvaluatorAgent evaluator, StoryWriterAgent storyWriter, TestCaseWriterAgent testCaseWriter,
        IAnalysisRepository repository, AnalysisOptions options)
    {
        _textExtractor = textExtractor;
        _extractor = extractor;
        _evaluator = evaluator;
        _storyWriter = storyWriter;
        _testCaseWriter = testCaseWriter;
        _repository = repository;
        _options = options;
    }

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
                if (!evaluation!.Passed)
                {
                    continue; // guardrail: sin historias para requerimientos que no pasan
                }

                IReadOnlyList<UserStory> stories = Array.Empty<UserStory>();
                try
                {
                    stories = await _storyWriter.WriteAsync(requirement, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { error = ex.Message; }
                if (error is not null)
                {
                    break;
                }

                for (int index = 0; index < stories.Count; index++)
                {
                    UserStory story = stories[index];
                    requirement.AddStory(story);
                    yield return AnalysisEvent.FromStory(requirement.Code, index, story);

                    TestCase? testCase = null;
                    try
                    {
                        testCase = await _testCaseWriter.WriteAsync(story, cancellationToken);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { error = ex.Message; }
                    if (error is not null)
                    {
                        break;
                    }

                    story.AttachTestCase(testCase!);
                    yield return AnalysisEvent.FromTestCase(requirement.Code, index, testCase!);
                }

                if (error is not null)
                {
                    break;
                }
            }
        }

        if (error is not null)
        {
            analysis.Fail(error);
            await _repository.SaveAsync(analysis, cancellationToken);
            yield return AnalysisEvent.Error(error);
            yield break;
        }

        analysis.Complete();
        await _repository.SaveAsync(analysis, cancellationToken);
        yield return AnalysisEvent.Done(analysis.Id);
    }
}
