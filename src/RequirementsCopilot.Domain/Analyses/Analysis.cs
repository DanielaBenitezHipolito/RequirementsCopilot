namespace RequirementsCopilot.Domain.Analyses;

public sealed class Analysis
{
    private readonly List<Requirement> _requirements = new();

    public Guid Id { get; }
    public string FileName { get; }
    public DateTime CreatedAt { get; }
    public AnalysisStatus Status { get; private set; }
    public string? Error { get; private set; }
    public IReadOnlyList<Requirement> Requirements => _requirements;

    private Analysis(Guid id, string fileName, DateTime createdAt, AnalysisStatus status, string? error)
        => (Id, FileName, CreatedAt, Status, Error) = (id, fileName, createdAt, status, error);

    public static Analysis Create(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("El análisis requiere nombre de archivo.", nameof(fileName));
        }
        return new Analysis(Guid.NewGuid(), fileName.Trim(), DateTime.UtcNow, AnalysisStatus.Processing, null);
    }

    public static Analysis Rehydrate(Guid id, string fileName, DateTime createdAt, AnalysisStatus status,
        string? error, IReadOnlyList<Requirement> requirements)
    {
        var analysis = new Analysis(id, fileName, createdAt, status, error);
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
}
