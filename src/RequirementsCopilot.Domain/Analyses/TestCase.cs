namespace RequirementsCopilot.Domain.Analyses;

public sealed record TestCase
{
    public string Title { get; }
    public IReadOnlyList<string> Preconditions { get; }
    public IReadOnlyList<string> Steps { get; }
    public string ExpectedResult { get; }

    private TestCase(string title, IReadOnlyList<string> preconditions, IReadOnlyList<string> steps, string expectedResult)
        => (Title, Preconditions, Steps, ExpectedResult) = (title, preconditions, steps, expectedResult);

    public static TestCase Create(string title, IReadOnlyList<string> preconditions, IReadOnlyList<string> steps, string expectedResult)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("El caso de prueba requiere título.", nameof(title));
        }
        return new TestCase(title.Trim(), preconditions?.ToArray() ?? Array.Empty<string>(),
            steps?.ToArray() ?? Array.Empty<string>(), expectedResult?.Trim() ?? string.Empty);
    }
}
