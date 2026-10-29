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
                ["agente-x"] = new() { Model = "gpt-5-mini" },
            },
        },
    };

    /// <summary>Catálogo real sobre una carpeta temporal con el prompt de «agente-x».</summary>
    private static FilePromptCatalog Catalog()
    {
        string dir = Path.Combine(Path.GetTempPath(), "prompts-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "agente-x.md"), "Eres el agente X.");
        return new FilePromptCatalog(dir);
    }

    [Fact]
    public async Task CompleteAsync_EnviaInstructionsDelCatalogoSinAgentReference()
    {
        var handler = new RecordingHandler { ResponseBody = ResponsesReply };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options(), Catalog());

        var result = await chat.CompleteAsync(new ChatPrompt("agente-x", "entrada", "resp_prev"));

        Assert.Equal("{\"ok\":true}", result.Text);
        Assert.Equal("resp_123", result.ResponseId);
        Assert.Equal(
            "https://demo.services.ai.azure.com/api/projects/copilot/openai/v1/responses",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal("test-key", handler.Request.Headers.GetValues("api-key").Single());

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var root = body.RootElement;
        Assert.Equal("Eres el agente X.", root.GetProperty("instructions").GetString());
        Assert.False(root.TryGetProperty("agent_reference", out _));
        Assert.Equal("gpt-5-mini", root.GetProperty("model").GetString()); // override por agente
        Assert.Equal("resp_prev", root.GetProperty("previous_response_id").GetString());
        Assert.Equal("entrada",
            root.GetProperty("input")[0].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task CompleteAsync_AgenteSinOverride_UsaModeloPorDefecto()
    {
        string dir = Path.Combine(Path.GetTempPath(), "prompts-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "agente-simple.md"), "Prompt simple.");
        var handler = new RecordingHandler { ResponseBody = ResponsesReply };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options(), new FilePromptCatalog(dir));

        await chat.CompleteAsync(new ChatPrompt("agente-simple", "entrada"));

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var root = body.RootElement;
        Assert.Equal("gpt-4.1-mini", root.GetProperty("model").GetString());
        Assert.Equal("Prompt simple.", root.GetProperty("instructions").GetString());
        Assert.False(root.TryGetProperty("previous_response_id", out _));
        Assert.False(root.TryGetProperty("max_output_tokens", out _)); // sin tope configurado no se envía
    }

    [Fact]
    public async Task CompleteAsync_AgenteSinPrompt_LanzaSinLlamarALaApi()
    {
        var handler = new RecordingHandler { ResponseBody = ResponsesReply };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options(), Catalog());

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => chat.CompleteAsync(new ChatPrompt("agente-fantasma", "entrada")));
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task CompleteAsync_ConMaxOutputTokens_LoEnviaEnElPayload()
    {
        var options = Options();
        options.Chat.MaxOutputTokens = 1234;
        var handler = new RecordingHandler { ResponseBody = ResponsesReply };
        var chat = new FoundryChatCompletion(new HttpClient(handler), options, Catalog());

        await chat.CompleteAsync(new ChatPrompt("agente-x", "entrada"));

        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(1234, body.RootElement.GetProperty("max_output_tokens").GetInt32());
    }

    [Fact]
    public async Task CompleteAsync_ConImagenes_EmiteInputImageJuntoAlTexto()
    {
        var handler = new RecordingHandler { ResponseBody = ResponsesReply };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options(), Catalog());
        var imagen = new ImageAttachment("image/png", "AAAA");

        await chat.CompleteAsync(new ChatPrompt("agente-x", "entrada", null, new[] { imagen }));

        using var body = JsonDocument.Parse(handler.RequestBody!);
        JsonElement content = body.RootElement.GetProperty("input")[0].GetProperty("content");
        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal("input_text", content[0].GetProperty("type").GetString());
        Assert.Equal("entrada", content[0].GetProperty("text").GetString());
        Assert.Equal("input_image", content[1].GetProperty("type").GetString());
        Assert.Equal("data:image/png;base64,AAAA", content[1].GetProperty("image_url").GetString());
    }

    [Fact]
    public async Task CompleteAsync_SinImagenes_MantienePayloadSoloTexto()
    {
        var handler = new RecordingHandler { ResponseBody = ResponsesReply };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options(), Catalog());

        await chat.CompleteAsync(new ChatPrompt("agente-x", "entrada"));

        using var body = JsonDocument.Parse(handler.RequestBody!);
        JsonElement content = body.RootElement.GetProperty("input")[0].GetProperty("content");
        Assert.Equal(1, content.GetArrayLength());
        Assert.Equal("input_text", content[0].GetProperty("type").GetString());
    }

    private sealed class RecordingUsage : RequirementsCopilot.Application.Usage.ILlmUsageRepository
    {
        public List<RequirementsCopilot.Application.Usage.LlmUsage> Entries { get; } = new();
        public Task AddAsync(RequirementsCopilot.Application.Usage.LlmUsage usage, CancellationToken ct = default)
        {
            Entries.Add(usage);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<RequirementsCopilot.Application.Usage.LlmUsage>> GetSinceAsync(
            DateTime since, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RequirementsCopilot.Application.Usage.LlmUsage>>(Entries.ToArray());
    }

    [Fact]
    public async Task CompleteAsync_RespuestaConUsage_RegistraTokens()
    {
        var handler = new RecordingHandler
        {
            ResponseBody = "{\"id\":\"resp_1\",\"usage\":{\"input_tokens\":120,\"output_tokens\":45}," +
                "\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"ok\"}]}]}",
        };
        var usage = new RecordingUsage();
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options(), Catalog(), usage);

        await chat.CompleteAsync(new ChatPrompt("agente-x", "entrada"));

        var entry = Assert.Single(usage.Entries);
        Assert.Equal("agente-x", entry.Agent);
        Assert.Equal("gpt-5-mini", entry.Model); // override por agente
        Assert.Equal(120, entry.InputTokens);
        Assert.Equal(45, entry.OutputTokens);
    }

    [Fact]
    public async Task CompleteAsync_SinRepositorioDeUso_NoFalla()
    {
        var handler = new RecordingHandler { ResponseBody = ResponsesReply };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options(), Catalog());
        var result = await chat.CompleteAsync(new ChatPrompt("agente-x", "entrada"));
        Assert.Equal("{\"ok\":true}", result.Text);
    }

    [Fact]
    public async Task CompleteAsync_RespuestaSinOutput_DevuelveVacio()
    {
        var handler = new RecordingHandler { ResponseBody = "{}" };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options(), Catalog());
        var result = await chat.CompleteAsync(new ChatPrompt("agente-x", "in"));
        Assert.Equal(string.Empty, result.Text);
        Assert.Null(result.ResponseId);
    }

    private sealed class FlakyHandler : HttpMessageHandler
    {
        private readonly int _failures;

        public FlakyHandler(int failures) => _failures = failures;

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (++Calls <= _failures)
            {
                throw new HttpRequestException("An error occurred while sending the request.",
                    new IOException("conexión reiniciada por el host remoto"));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponsesReply, Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public async Task CompleteAsync_FalloTransitorio_ReintentaYCompleta()
    {
        var handler = new FlakyHandler(failures: 1);
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options(), Catalog());

        var result = await chat.CompleteAsync(new ChatPrompt("agente-x", "entrada"));

        Assert.Equal("{\"ok\":true}", result.Text);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task CompleteAsync_FalloPersistente_LanzaConCausaInterna()
    {
        var handler = new FlakyHandler(failures: 5);
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options(), Catalog());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => chat.CompleteAsync(new ChatPrompt("agente-x", "entrada")));

        Assert.Contains("Foundry", ex.Message);
        Assert.Contains("conexión reiniciada por el host remoto", ex.Message);
        Assert.Equal(2, handler.Calls); // solo un reintento, sin loop infinito
    }
}
