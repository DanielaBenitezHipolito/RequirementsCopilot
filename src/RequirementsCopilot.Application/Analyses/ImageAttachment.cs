namespace RequirementsCopilot.Application.Analyses;

/// <summary>
/// Imagen adjunta a un prompt multimodal (captura, mockup, diagrama), codificada en base64.
/// Formato de intercambio único en todo el pipeline: extractores de documentos, chat y Foundry.
/// </summary>
/// <param name="MimeType">Tipo MIME de la imagen (p. ej. <c>image/png</c>).</param>
/// <param name="Base64Data">Contenido de la imagen codificado en base64, sin prefijo de data URL.</param>
public sealed record ImageAttachment(string MimeType, string Base64Data)
{
    /// <summary>Representa la imagen como data URL (<c>data:{mime};base64,{datos}</c>), el formato que acepta la Responses API.</summary>
    public string ToDataUrl() => $"data:{MimeType};base64,{Base64Data}";

    /// <summary>Crea el adjunto codificando <paramref name="data"/> en base64.</summary>
    /// <param name="mimeType">Tipo MIME de la imagen.</param>
    /// <param name="data">Bytes crudos de la imagen.</param>
    public static ImageAttachment FromBytes(string mimeType, byte[] data)
        => new(mimeType, Convert.ToBase64String(data));

    /// <summary>
    /// Parsea una data URL de imagen (<c>data:image/…;base64,…</c>), el formato en que el frontend
    /// envía los adjuntos. Lanza <see cref="ArgumentException"/> si no es una imagen base64 válida.
    /// </summary>
    /// <param name="dataUrl">Data URL a parsear.</param>
    public static ImageAttachment FromDataUrl(string dataUrl)
    {
        const string marker = ";base64,";
        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La imagen debe ser una data URL (data:image/…;base64,…).", nameof(dataUrl));
        }
        int markerIndex = dataUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0 || markerIndex + marker.Length >= dataUrl.Length)
        {
            throw new ArgumentException("La imagen debe venir codificada en base64 y no estar vacía.", nameof(dataUrl));
        }
        string mimeType = dataUrl["data:".Length..markerIndex];
        string base64 = dataUrl[(markerIndex + marker.Length)..];
        return new ImageAttachment(mimeType, base64);
    }
}
