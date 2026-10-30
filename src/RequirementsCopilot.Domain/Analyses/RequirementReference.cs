namespace RequirementsCopilot.Domain.Analyses;

/// <summary>
/// Documento de referencia adjunto a UN requerimiento (p. ej. el PDF modelo que el formulario debe
/// producir): material de solo lectura que guía al evaluador y a los redactores de entregables.
/// </summary>
/// <param name="Name">Nombre del archivo original.</param>
/// <param name="Content">Texto extraído del documento.</param>
public sealed record RequirementReference(string Name, string Content);
