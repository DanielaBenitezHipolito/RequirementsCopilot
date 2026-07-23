namespace RequirementsCopilot.Domain.Analyses;

/// <summary>Un paso numerado dentro de un flujo del proceso: acción del actor y resultado esperado del sistema.</summary>
public sealed record PasoFlujo(int Numero, string Accion, string ResultadoEsperado);
