namespace RequirementsCopilot.Domain.Analyses;

public sealed class Clarification
{
    public string Question { get; }
    public string? Answer { get; private set; }

    /// <summary>Opciones sugeridas de respuesta (vacío si la pregunta es abierta).</summary>
    public IReadOnlyList<string> Options { get; }

    private Clarification(string question, string? answer, IReadOnlyList<string> options)
        => (Question, Answer, Options) = (question, answer, options);

    public static Clarification Create(string question, IReadOnlyList<string>? options = null)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("La pregunta de clarificación no puede estar vacía.", nameof(question));
        }
        return new Clarification(question.Trim(), null, Normalize(options));
    }

    public static Clarification Rehydrate(string question, string? answer, IReadOnlyList<string>? options = null)
        => new(question, answer, Normalize(options));

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? options)
        => options?.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToArray()
           ?? Array.Empty<string>();

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
