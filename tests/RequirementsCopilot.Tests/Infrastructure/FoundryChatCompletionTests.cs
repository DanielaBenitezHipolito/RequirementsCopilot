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

    private static FoundryOptions Options() => new()
    {
        Endpoint = "https://demo.openai.azure.com",
        ApiKey = "test-key",
        Deployment = "gpt-4.1-mini",
    };

    [Fact]
    public async Task CompleteAsync_ArmaRequestYExtraeContenido()
    {
        var handler = new RecordingHandler
        {
            ResponseBody = "{\"choices\":[{\"message\":{\"content\":\"{\\\"ok\\\":true}\"}}]}",
        };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options());

        var result = await chat.CompleteAsync(new ChatPrompt("agente-x", "instrucciones del agente", "entrada del usuario"));

        Assert.Equal("{\"ok\":true}", result.Text);
        Assert.Equal(
            "https://demo.openai.azure.com/openai/deployments/gpt-4.1-mini/chat/completions?api-version=2024-10-21",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal("test-key", handler.Request.Headers.GetValues("api-key").Single());

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("instrucciones del agente", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
    }

    [Fact]
    public async Task CompleteAsync_RespuestaSinChoices_DevuelveVacio()
    {
        var handler = new RecordingHandler { ResponseBody = "{}" };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options());
        var result = await chat.CompleteAsync(new ChatPrompt("a", "i", "in"));
        Assert.Equal(string.Empty, result.Text);
    }
}
