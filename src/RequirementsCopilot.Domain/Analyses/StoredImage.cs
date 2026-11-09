namespace RequirementsCopilot.Domain.Analyses;

/// <summary>
/// Imagen persistida (reducida) para mostrarla en la aplicación e incrustarla en los documentos
/// exportados: las del documento analizado viven en el análisis; las adjuntadas a mano, en su
/// requerimiento.
/// </summary>
/// <param name="Name">Nombre para mostrar (archivo original o secuencial).</param>
/// <param name="MimeType">Tipo MIME (image/png, image/jpeg…).</param>
/// <param name="Base64Data">Contenido en base64.</param>
public sealed record StoredImage(string Name, string MimeType, string Base64Data);
