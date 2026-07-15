namespace RequirementsCopilot.Domain.Analyses;

public sealed class Requirement
{
    private readonly List<UserStory> _stories = new();

    public string Code { get; }
    public string Text { get; }
    public string Area { get; }
    public Evaluation? Evaluation { get; private set; }
    public IReadOnlyList<UserStory> Stories => _stories;

    private Requirement(string code, string text, string area) => (Code, Text, Area) = (code, text, area);

    public static Requirement Create(string code, string text, string area)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("El requerimiento requiere código.", nameof(code));
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("El requerimiento requiere texto.", nameof(text));
        }
        return new Requirement(code.Trim(), text.Trim(), string.IsNullOrWhiteSpace(area) ? "General" : area.Trim());
    }

    public void Evaluate(Evaluation evaluation) => Evaluation = evaluation;

    public void AddStory(UserStory story) => _stories.Add(story);
}
