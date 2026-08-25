namespace RequirementsCopilot.Domain.Analyses;

public sealed class Analysis
{
    private readonly List<Requirement> _requirements = new();

    public Guid Id { get; }
    public string FileName { get; }
    public DateTime CreatedAt { get; }
    public AnalysisStatus Status { get; private set; }
    public string? Error { get; private set; }
    public string? Summary { get; private set; }

    /// <summary>Proyecto existente al que pertenece (contexto para los agentes); null = proyecto nuevo.</summary>
    public string? ProjectName { get; }
    public IReadOnlyList<Requirement> Requirements => _requirements;

    private Analysis(Guid id, string fileName, DateTime createdAt, AnalysisStatus status, string? error, string? summary,
        string? projectName)
        => (Id, FileName, CreatedAt, Status, Error, Summary, ProjectName)
            = (id, fileName, createdAt, status, error, summary, NormalizeProject(projectName));

    private static string? NormalizeProject(string? projectName)
        => string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim();

    public static Analysis Create(string fileName, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("El análisis requiere nombre de archivo.", nameof(fileName));
        }
        return new Analysis(Guid.NewGuid(), fileName.Trim(), DateTime.UtcNow, AnalysisStatus.Processing, null, null, projectName);
    }

    public static Analysis Rehydrate(Guid id, string fileName, DateTime createdAt, AnalysisStatus status,
        string? error, string? summary, IReadOnlyList<Requirement> requirements, string? projectName = null)
    {
        var analysis = new Analysis(id, fileName, createdAt, status, error, summary, projectName);
        analysis._requirements.AddRange(requirements);
        return analysis;
    }

    public void AddRequirement(Requirement requirement) => _requirements.Add(requirement);

    public void Complete() => Status = AnalysisStatus.Completed;

    public void Fail(string error)
    {
        Status = AnalysisStatus.Failed;
        Error = error;
    }

    public void SetSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("El resumen no puede estar vacío.", nameof(summary));
        }
        Summary = summary.Trim();
    }
}
