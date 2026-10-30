namespace RequirementsCopilot.Application.Projects;

/// <summary>
/// Documento de ejemplo de un proyecto (plantilla, entregable modelo): material de referencia de
/// solo lectura que los agentes usan como guía de formato y tono, separado del contenido editable.
/// </summary>
/// <param name="Name">Nombre del archivo original.</param>
/// <param name="Content">Texto extraído del documento.</param>
public sealed record ProjectExample(string Name, string Content);
