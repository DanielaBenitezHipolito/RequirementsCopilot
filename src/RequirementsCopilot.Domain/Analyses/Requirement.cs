namespace RequirementsCopilot.Domain.Analyses;

/// <summary>Requerimiento extraído de un documento, con su evaluación, clarificaciones y caso de uso generado.</summary>
public sealed class Requirement
{
    private readonly List<Clarification> _clarifications = new();
    private readonly List<UserStory> _userStories = new();

    /// <summary>Código secuencial del requerimiento (ej. REQ-001).</summary>
    public string Code { get; }

    /// <summary>Texto del requerimiento.</summary>
    public string Text { get; }

    /// <summary>Área funcional a la que pertenece.</summary>
    public string Area { get; }

    /// <summary>Evaluación de calidad más reciente, si ya fue evaluado.</summary>
    public Evaluation? Evaluation { get; private set; }

    /// <summary>Caso de uso generado para este requerimiento, si ya se generó.</summary>
    public UseCase? UseCase { get; private set; }

    /// <summary>Preguntas de clarificación asociadas al requerimiento.</summary>
    public IReadOnlyList<Clarification> Clarifications => _clarifications;

    /// <summary>Historias de usuario generadas, si ya se generaron.</summary>
    public IReadOnlyList<UserStory> UserStories => _userStories;

    /// <summary>Aprobado directo, o con todas sus preguntas de clarificación respondidas.</summary>
    public bool ReadyForStories => Evaluation is not null &&
        (Evaluation.Passed || (_clarifications.Count > 0 && _clarifications.All(c => c.IsAnswered)));

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

    /// <summary>Registra la evaluación de calidad del requerimiento.</summary>
    public void Evaluate(Evaluation evaluation) => Evaluation = evaluation;

    /// <summary>Asigna el caso de uso generado para este requerimiento.</summary>
    public void SetUseCase(UseCase useCase) => UseCase = useCase;

    /// <summary>Agrega una pregunta de clarificación al requerimiento.</summary>
    public void AddClarification(Clarification clarification) => _clarifications.Add(clarification);

    /// <summary>Reemplaza las historias de usuario generadas para este requerimiento.</summary>
    public void SetUserStories(IReadOnlyList<UserStory> stories)
    {
        if (stories is null || stories.Count == 0)
        {
            throw new ArgumentException("Debe asignar al menos una historia.", nameof(stories));
        }
        _userStories.Clear();
        _userStories.AddRange(stories);
    }
}
