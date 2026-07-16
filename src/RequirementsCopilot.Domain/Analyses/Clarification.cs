namespace RequirementsCopilot.Domain.Analyses;

public sealed class Clarification
{
    public string Question { get; }
    public string? Answer { get; private set; }

    private Clarification(string question, string? answer) => (Question, Answer) = (question, answer);

    public static Clarification Create(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("La pregunta de clarificación no puede estar vacía.", nameof(question));
        }
        return new Clarification(question.Trim(), null);
    }

    public static Clarification Rehydrate(string question, string? answer) => new(question, answer);

    public bool IsAnswered => !string.IsNullOrWhiteSpace(Answer);

    public void Respond(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new ArgumentException("La respuesta no puede estar vacía.", nameof(answer));
        }
        Answer = answer.Trim();
    }
}
