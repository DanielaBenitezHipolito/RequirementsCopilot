using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Chat;

/// <summary>
/// Adaptador de <see cref="IChatCompletion"/> sobre Azure AI Foundry (Responses API), invocando un
/// agente publicado por referencia (<c>agent_reference</c>) y manteniendo el hilo con
/// <c>previous_response_id</c>. Espeja el patrón de JYDE.OpenDataCopilot:
/// <c>POST {endpoint}/openai/v1/responses</c> con cabecera <c>api-key</c>.
/// </summary>
public sealed class FoundryChatCompletion : IChatCompletion
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly FoundryOptions _options;

    public FoundryChatCompletion(HttpClient httpClient, FoundryOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _options = options;

        if (!string.IsNullOrWhiteSpace(options.ApiKey) && !_httpClient.DefaultRequestHeaders.Contains("api-key"))
        {
            _httpClient.DefaultRequestHeaders.Add("api-key", options.ApiKey);
        }
    }

    public async Task<ChatResult> CompleteAsync(ChatPrompt prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        FoundryAgentSettings? agent = _options.Chat.Agents.GetValueOrDefault(prompt.Agent);
        string model = agent?.Model ?? _options.Chat.Model;
        string agentName = agent?.Name ?? prompt.Agent;

        Dictionary<string, object> agentReference = new()
        {
            ["name"] = agentName,
            ["type"] = "agent_reference",
        };
        if (!string.IsNullOrWhiteSpace(agent?.Version))
        {
            agentReference["version"] = agent.Version;
        }

        Dictionary<string, object> payload = new()
        {
            ["model"] = model,
            ["input"] = new[]
            {
                new
                {
                    role = "user",
                    type = "message",
                    content = new[] { new { type = "input_text", text = prompt.Input } },
                },
            },
            ["agent_reference"] = agentReference,
        };
        if (!string.IsNullOrWhiteSpace(prompt.PreviousResponseId))
        {
            payload["previous_response_id"] = prompt.PreviousResponseId;
        }

        Uri url = new($"{_options.Endpoint.TrimEnd('/')}/openai/v1/responses");

        // Un blip de red no debe tumbar el turno/análisis completo: un reintento con pausa corta
        // absorbe fallos transitorios; el segundo fallo se propaga con la causa interna visible.
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await SendAsync(url, payload, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (attempt >= 2)
                {
                    throw new InvalidOperationException($"Error llamando a Foundry: {Describe(ex)}", ex);
                }
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private async Task<ChatResult> SendAsync(Uri url, Dictionary<string, object> payload, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        string text = ExtractText(root);
        string? responseId = root.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() : null;
        return new ChatResult(text, responseId);
    }

    /// <summary>Mensaje del error incluyendo las causas internas (DNS, timeout, TLS…).</summary>
    private static string Describe(Exception ex)
        => ex.InnerException is null ? ex.Message : $"{ex.Message} ({Describe(ex.InnerException)})";

    /// <summary>Extrae el texto de la respuesta de la Responses API (output[].content[].output_text).</summary>
    private static string ExtractText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out JsonElement output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (JsonElement item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out JsonElement type) &&
                    type.GetString() == "output_text" &&
                    part.TryGetProperty("text", out JsonElement text))
                {
                    builder.Append(text.GetString());
                }
            }
        }

        return builder.ToString();
    }
}
