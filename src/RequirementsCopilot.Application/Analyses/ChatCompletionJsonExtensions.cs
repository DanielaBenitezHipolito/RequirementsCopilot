namespace RequirementsCopilot.Application.Analyses;

/// <summary>
/// Llamada al LLM esperando un objeto JSON. Ante una respuesta sin JSON (flakiness ocasional
/// del modelo) reintenta UNA vez antes de rendirse; el agente decide el mensaje de error.
/// </summary>
public static class ChatCompletionJsonExtensions
{
    public static async Task<string?> CompleteJsonAsync(this IChatCompletion chat, ChatPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        ChatResult result = await chat.CompleteAsync(prompt, cancellationToken);
        string? json = JsonText.FirstJsonObject(result.Text);
        if (json is not null)
        {
            return json;
        }

        result = await chat.CompleteAsync(prompt, cancellationToken);
        return JsonText.FirstJsonObject(result.Text);
    }
}
