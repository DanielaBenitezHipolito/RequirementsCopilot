namespace RequirementsCopilot.Domain.Analyses;

/// <summary>Un flujo del proceso del caso de uso (p. ej. "Proceso de creación manual"), con sus pasos numerados.</summary>
public sealed record FlujoProceso(string Titulo, IReadOnlyList<PasoFlujo> Pasos);
