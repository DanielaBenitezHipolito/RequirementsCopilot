using System.Text.Json;

namespace RequirementsCopilot.Application.Analyses;

/// <summary>
/// Llamada al LLM esperando un objeto JSON. Ante una respuesta sin JSON o con JSON malformado
/// (flakiness ocasional del modelo: comillas sin escapar, llaves de más…) reintenta UNA vez
/// antes de rendirse; el agente decide el mensaje de error si aun así no llega JSON válido.
/// </summary>
public static class ChatCompletionJsonExtensions
{
    public static async Task<string?> CompleteJsonAsync(this IChatCompletion chat, ChatPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        string? json = Extract((await chat.CompleteAsync(prompt, cancellationToken)).Text);
        if (json is not null)
        {
            return json;
        }

        // Segundo intento: el modelo suele producir JSON bien formado al reintentar.
        return Extract((await chat.CompleteAsync(prompt, cancellationToken)).Text);
    }

    /// <summary>Primer objeto JSON del texto que además sea parseable; null si no hay uno válido.</summary>
    private static string? Extract(string text)
    {
        string? candidate = JsonText.FirstJsonObject(text);
        if (candidate is null)
        {
            return null;
        }
        try
        {
            using JsonDocument _ = JsonDocument.Parse(candidate);
            return candidate;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
