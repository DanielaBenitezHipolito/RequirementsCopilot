using System.Net;
using System.Text;
using System.Text.Json;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Infrastructure.Chat;

namespace RequirementsCopilot.Tests.Infrastructure;

public class FoundryChatCompletionTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }
        public string ResponseBody { get; set; } = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private const string ResponsesReply =
        "{\"id\":\"resp_123\",\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"{\\\"ok\\\":true}\"}]}]}";

    private static FoundryOptions Options() => new()
    {
        Endpoint = "https://demo.services.ai.azure.com/api/projects/copilot",
        ApiKey = "test-key",
        Chat = new FoundryChatSettings
        {
            Model = "gpt-4.1-mini",
            Agents = new Dictionary<string, FoundryAgentSettings>
            {
                ["agente-x"] = new() { Name = "agente-x", Version = "2" },
            },
        },
    };

    [Fact]
    public async Task CompleteAsync_AgentePublicado_UsaAgentReferenceYExtraeTexto()
    {
        var handler = new RecordingHandler { ResponseBody = ResponsesReply };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options());

        var result = await chat.CompleteAsync(new ChatPrompt("agente-x", "entrada", "resp_prev"));

        Assert.Equal("{\"ok\":true}", result.Text);
        Assert.Equal("resp_123", result.ResponseId);
        Assert.Equal(
            "https://demo.services.ai.azure.com/api/projects/copilot/openai/v1/responses",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal("test-key", handler.Request.Headers.GetValues("api-key").Single());

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var root = body.RootElement;
        Assert.Equal("gpt-4.1-mini", root.GetProperty("model").GetString());
        Assert.Equal("agente-x", root.GetProperty("agent_reference").GetProperty("name").GetString());
        Assert.Equal("agent_reference", root.GetProperty("agent_reference").GetProperty("type").GetString());
        Assert.Equal("2", root.GetProperty("agent_reference").GetProperty("version").GetString());
        Assert.Equal("resp_prev", root.GetProperty("previous_response_id").GetString());
        Assert.Equal("entrada",
            root.GetProperty("input")[0].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task CompleteAsync_AgenteNoEnCatalogo_UsaNombreLogicoYModeloPorDefecto()
    {
        var handler = new RecordingHandler { ResponseBody = ResponsesReply };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options());

        await chat.CompleteAsync(new ChatPrompt("agente-desconocido", "entrada"));

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var root = body.RootElement;
        Assert.Equal("gpt-4.1-mini", root.GetProperty("model").GetString());
        Assert.Equal("agente-desconocido", root.GetProperty("agent_reference").GetProperty("name").GetString());
        Assert.False(root.GetProperty("agent_reference").TryGetProperty("version", out _));
        Assert.False(root.TryGetProperty("previous_response_id", out _));
    }

    [Fact]
    public async Task CompleteAsync_RespuestaSinOutput_DevuelveVacio()
    {
        var handler = new RecordingHandler { ResponseBody = "{}" };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options());
        var result = await chat.CompleteAsync(new ChatPrompt("a", "in"));
        Assert.Equal(string.Empty, result.Text);
        Assert.Null(result.ResponseId);
    }
}
