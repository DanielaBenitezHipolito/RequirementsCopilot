namespace RequirementsCopilot.Domain.Analyses;

public sealed class UserStory
{
    public string Role { get; }
    public string Goal { get; }
    public string Benefit { get; }
    public IReadOnlyList<string> AcceptanceCriteria { get; }
    public TestCase? TestCase { get; private set; }

    private UserStory(string role, string goal, string benefit, IReadOnlyList<string> acceptanceCriteria)
        => (Role, Goal, Benefit, AcceptanceCriteria) = (role, goal, benefit, acceptanceCriteria);

    public static UserStory Create(string role, string goal, string benefit, IReadOnlyList<string> acceptanceCriteria)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            throw new ArgumentException("La historia requiere el objetivo (quiero).", nameof(goal));
        }
        return new UserStory(role?.Trim() ?? string.Empty, goal.Trim(), benefit?.Trim() ?? string.Empty,
            acceptanceCriteria?.ToArray() ?? Array.Empty<string>());
    }

    public void AttachTestCase(TestCase testCase) => TestCase = testCase;
}
