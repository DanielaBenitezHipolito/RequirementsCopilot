namespace RequirementsCopilot.Application.Analyses;

/// <summary>
/// Contenido extraído de un documento subido: el texto plano y las imágenes embebidas
/// (capturas, mockups, diagramas) que acompañan al análisis multimodal.
/// </summary>
/// <param name="Text">Texto plano del documento (vacío si es una imagen suelta o un escaneo sin OCR).</param>
/// <param name="Images">Imágenes embebidas o adjuntas, listas para enviar al LLM.</param>
public sealed record DocumentContent(string Text, IReadOnlyList<ImageAttachment> Images)
{
    /// <summary>Contenido de solo texto, sin imágenes.</summary>
    /// <param name="text">Texto plano del documento.</param>
    public static DocumentContent FromText(string text) => new(text, Array.Empty<ImageAttachment>());
}
