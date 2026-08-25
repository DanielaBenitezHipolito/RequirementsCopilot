namespace RequirementsCopilot.Application.Analyses;

public sealed record AnalysisOptions
{
    public double PassThreshold { get; init; } = 3.5;

    /// <summary>Tope de caracteres del documento enviados al extractor (control de costos).</summary>
    public int MaxInputChars { get; init; } = 60_000;

    /// <summary>Tope de caracteres del .md de proyecto inyectado como contexto a los agentes.</summary>
    public int MaxProjectContextChars { get; init; } = 60_000;
}
