using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Infrastructure.Mongo;

public sealed class AnalysisDocument
{
    // MongoDB.Driver 3.x no serializa Guid sin representación explícita.
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public List<RequirementDocument> Requirements { get; set; } = new();

    public static AnalysisDocument FromDomain(Analysis analysis) => new()
    {
        Id = analysis.Id,
        FileName = analysis.FileName,
        CreatedAt = analysis.CreatedAt,
        Status = analysis.Status.ToString(),
        Error = analysis.Error,
        Requirements = analysis.Requirements.Select(RequirementDocument.FromDomain).ToList(),
    };

    public Analysis ToDomain() => Analysis.Rehydrate(
        Id, FileName, CreatedAt, Enum.Parse<AnalysisStatus>(Status), Error,
        Requirements.Select(r => r.ToDomain()).ToArray());
}

public sealed class RequirementDocument
{
    public string Code { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public EvaluationDocument? Evaluation { get; set; }
    public List<ClarificationDocument> Clarifications { get; set; } = new();
    public List<StoryDocument> Stories { get; set; } = new();

    public static RequirementDocument FromDomain(Requirement requirement) => new()
    {
        Code = requirement.Code,
        Text = requirement.Text,
        Area = requirement.Area,
        Evaluation = requirement.Evaluation is null ? null : EvaluationDocument.FromDomain(requirement.Evaluation),
        Clarifications = requirement.Clarifications.Select(c => new ClarificationDocument
        {
            Question = c.Question, Answer = c.Answer,
        }).ToList(),
        Stories = requirement.Stories.Select(StoryDocument.FromDomain).ToList(),
    };

    public Requirement ToDomain()
    {
        var requirement = Requirement.Create(Code, Text, Area);
        if (Evaluation is not null)
        {
            requirement.Evaluate(Evaluation.ToDomain());
        }
        foreach (ClarificationDocument clarification in Clarifications)
        {
            requirement.AddClarification(Domain.Analyses.Clarification.Rehydrate(clarification.Question, clarification.Answer));
        }
        foreach (StoryDocument story in Stories)
        {
            requirement.AddStory(story.ToDomain());
        }
        return requirement;
    }
}

public sealed class ClarificationDocument
{
    public string Question { get; set; } = string.Empty;
    public string? Answer { get; set; }
}

public sealed class EvaluationDocument
{
    public List<CriterionDocument> Scores { get; set; } = new();
    public double Threshold { get; set; }

    public static EvaluationDocument FromDomain(Evaluation evaluation) => new()
    {
        Scores = evaluation.Scores.Select(s => new CriterionDocument
        {
            Criterion = s.Criterion, Score = s.Score, Observation = s.Observation,
        }).ToList(),
        Threshold = evaluation.Threshold,
    };

    public Evaluation ToDomain() => Evaluation.Create(
        Scores.Select(s => CriterionScore.Create(s.Criterion, s.Score, s.Observation)).ToArray(), Threshold);
}

public sealed class CriterionDocument
{
    public string Criterion { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Observation { get; set; } = string.Empty;
}

public sealed class StoryDocument
{
    public string Role { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string Benefit { get; set; } = string.Empty;
    public List<string> AcceptanceCriteria { get; set; } = new();
    public TestCaseDocument? TestCase { get; set; }

    public static StoryDocument FromDomain(UserStory story) => new()
    {
        Role = story.Role,
        Goal = story.Goal,
        Benefit = story.Benefit,
        AcceptanceCriteria = story.AcceptanceCriteria.ToList(),
        TestCase = story.TestCase is null ? null : TestCaseDocument.FromDomain(story.TestCase),
    };

    public UserStory ToDomain()
    {
        var story = UserStory.Create(Role, Goal, Benefit, AcceptanceCriteria);
        if (TestCase is not null)
        {
            story.AttachTestCase(TestCase.ToDomain());
        }
        return story;
    }
}

public sealed class TestCaseDocument
{
    public string Title { get; set; } = string.Empty;
    public List<string> Preconditions { get; set; } = new();
    public List<string> Steps { get; set; } = new();
    public string ExpectedResult { get; set; } = string.Empty;

    public static TestCaseDocument FromDomain(TestCase testCase) => new()
    {
        Title = testCase.Title,
        Preconditions = testCase.Preconditions.ToList(),
        Steps = testCase.Steps.ToList(),
        ExpectedResult = testCase.ExpectedResult,
    };

    public TestCase ToDomain() => TestCase.Create(Title, Preconditions, Steps, ExpectedResult);
}
