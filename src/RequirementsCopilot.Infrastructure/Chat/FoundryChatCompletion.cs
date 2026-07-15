using System.Net.Http.Json;
using System.Text.Json;
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Chat;

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

        var payload = new
        {
            messages = new object[]
            {
                new { role = "system", content = prompt.Instructions },
                new { role = "user", content = prompt.Input },
            },
            temperature = 0.2,
        };

        Uri url = new(
            $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/{_options.Deployment}/chat/completions?api-version={_options.ApiVersion}");
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        return new ChatResult(ExtractContent(document.RootElement));
    }

    private static string ExtractContent(JsonElement root)
    {
        if (root.TryGetProperty("choices", out JsonElement choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out JsonElement message) &&
            message.TryGetProperty("content", out JsonElement content))
        {
            return content.GetString() ?? string.Empty;
        }
        return string.Empty;
    }
}
