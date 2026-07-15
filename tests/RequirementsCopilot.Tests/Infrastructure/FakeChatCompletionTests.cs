using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Infrastructure.Chat;

namespace RequirementsCopilot.Tests.Infrastructure;

public class FakeChatCompletionTests
{
    private readonly FakeChatCompletion _fake = new();

    [Theory]
    [InlineData(RequirementExtractorAgent.AgentName)]
    [InlineData(RequirementEvaluatorAgent.AgentName)]
    [InlineData(StoryWriterAgent.AgentName)]
    [InlineData(TestCaseWriterAgent.AgentName)]
    public async Task CompleteAsync_CadaAgente_DevuelveJsonParseable(string agent)
    {
        var result = await _fake.CompleteAsync(new ChatPrompt(agent, "instr", "REQ-001 input"));
        Assert.NotNull(JsonText.FirstJsonObject(result.Text));
    }

    [Fact]
    public async Task CompleteAsync_PipelineCompletoConAgentesReales_Funciona()
    {
        var extractor = new RequirementExtractorAgent(_fake);
        var requirements = await extractor.ExtractAsync("cualquier documento");
        Assert.True(requirements.Count >= 2);

        var evaluator = new RequirementEvaluatorAgent(_fake);
        var first = await evaluator.EvaluateAsync(requirements[0], 3.5);
        var second = await evaluator.EvaluateAsync(requirements[1], 3.5);
        Assert.True(first.Passed);
        Assert.False(second.Passed);

        var stories = await new StoryWriterAgent(_fake).WriteAsync(requirements[0]);
        Assert.NotEmpty(stories);
        var testCase = await new TestCaseWriterAgent(_fake).WriteAsync(stories[0]);
        Assert.False(string.IsNullOrWhiteSpace(testCase.Title));
    }
}
