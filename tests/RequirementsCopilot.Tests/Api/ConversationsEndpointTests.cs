using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RequirementsCopilot.Tests.Api;

public class ConversationsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ConversationsEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task FlujoConversacional_IdeaAPreguntaABorradorAAnalisis()
    {
        var client = _factory.CreateClient();

        // Turno 1: idea → el agente pregunta
        var first = await client.PostAsJsonAsync("/api/conversations/messages",
            new { mensaje = "Quiero controlar los pagos de las reservas" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var turn1 = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(turn1.GetProperty("listo").GetBoolean());
        string threadId = turn1.GetProperty("previousResponseId").GetString()!;
        Assert.False(string.IsNullOrEmpty(threadId));

        // Turno 2: respuesta → el agente redacta el requerimiento
        var second = await client.PostAsJsonAsync("/api/conversations/messages",
            new { mensaje = "El recepcionista; monto, fecha y consecutivo", previousResponseId = threadId });
        var turn2 = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(turn2.GetProperty("listo").GetBoolean());
        string texto = turn2.GetProperty("requerimiento").GetProperty("texto").GetString()!;
        string area = turn2.GetProperty("requerimiento").GetProperty("area").GetString()!;

        // Completar: el borrador entra al pipeline (evaluación + clarificación)
        var complete = await client.PostAsJsonAsync("/api/conversations/complete", new { texto, area });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = await complete.Content.ReadFromJsonAsync<JsonElement>();
        string analysisId = completed.GetProperty("analysisId").GetString()!;

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        Assert.Equal("Completed", detail.GetProperty("status").GetString());
        var requirement = detail.GetProperty("requerimientos")[0];
        Assert.Equal("REQ-001", requirement.GetProperty("codigo").GetString());
        Assert.NotEqual(JsonValueKind.Null, requirement.GetProperty("evaluacion").ValueKind);
    }

    [Fact]
    public async Task SendMessage_SinMensaje_Devuelve400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/conversations/messages", new { mensaje = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Complete_SinTexto_Devuelve400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/conversations/complete", new { texto = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
