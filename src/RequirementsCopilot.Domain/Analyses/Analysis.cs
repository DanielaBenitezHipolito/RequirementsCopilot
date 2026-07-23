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
    public IReadOnlyList<Requirement> Requirements => _requirements;

    private Analysis(Guid id, string fileName, DateTime createdAt, AnalysisStatus status, string? error, string? summary)
        => (Id, FileName, CreatedAt, Status, Error, Summary) = (id, fileName, createdAt, status, error, summary);

    public static Analysis Create(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("El análisis requiere nombre de archivo.", nameof(fileName));
        }
        return new Analysis(Guid.NewGuid(), fileName.Trim(), DateTime.UtcNow, AnalysisStatus.Processing, null, null);
    }

    public static Analysis Rehydrate(Guid id, string fileName, DateTime createdAt, AnalysisStatus status,
        string? error, string? summary, IReadOnlyList<Requirement> requirements)
    {
        var analysis = new Analysis(id, fileName, createdAt, status, error, summary);
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
