using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RequirementsCopilot.Tests.Api;

public class ConversationsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ConversationsEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static string ResponseIdFrom(string sseBody)
    {
        Match match = Regex.Match(sseBody, "\"responseId\":\"([^\"]+)\"");
        Assert.True(match.Success, "El SSE no incluyó el responseId en el evento done.");
        return match.Groups[1].Value;
    }

    [Fact]
    public async Task FlujoConversacional_IdeaAPreguntaABorradorAAnalisis()
    {
        var client = _factory.CreateClient();

        // Turno 1: idea → el agente pregunta (solo token, aún no está listo)
        var first = await client.PostAsJsonAsync("/api/conversations/messages",
            new { mensaje = "Quiero controlar los pagos de las reservas" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("text/event-stream", first.Content.Headers.ContentType!.MediaType);
        string body1 = await first.Content.ReadAsStringAsync();
        Assert.Contains("event: token", body1);
        Assert.DoesNotContain("event: draft", body1);
        Assert.Contains("event: done", body1);
        string threadId = ResponseIdFrom(body1);
        Assert.False(string.IsNullOrEmpty(threadId));

        // Turno 2: respuesta → el agente redacta el requerimiento (token + draft + done)
        var second = await client.PostAsJsonAsync("/api/conversations/messages",
            new { mensaje = "El recepcionista; monto, fecha y consecutivo", previousResponseId = threadId });
        string body2 = await second.Content.ReadAsStringAsync();
        Assert.Contains("event: token", body2);
        Assert.Contains("event: draft", body2);
        Assert.Contains("event: done", body2);

        Match draftMatch = Regex.Match(body2, "\"requerimiento\":\\{\"texto\":\"([^\"]+)\",\"area\":\"([^\"]+)\"\\}");
        Assert.True(draftMatch.Success, "El SSE no incluyó el borrador en el evento draft.");
        string texto = draftMatch.Groups[1].Value;
        string area = draftMatch.Groups[2].Value;

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

    private static Guid ConversationIdFrom(string sseBody)
    {
        Match match = Regex.Match(sseBody, "\"conversationId\":\"([^\"]+)\"");
        Assert.True(match.Success, "El SSE no incluyó el evento conversation con el conversationId.");
        return Guid.Parse(match.Groups[1].Value);
    }

    [Fact]
    public async Task Conversacion_SePersisteDesdePrimerMensaje_YPermiteReabrirYCompletar()
    {
        var client = _factory.CreateClient();

        // 1er mensaje sin conversationId → el SSE incluye el evento conversation con un id nuevo.
        var first = await client.PostAsJsonAsync("/api/conversations/messages",
            new { mensaje = "Quiero controlar los pagos de las reservas" });
        string body1 = await first.Content.ReadAsStringAsync();
        Assert.Contains("event: conversation", body1);
        Guid conversationId = ConversationIdFrom(body1);
        string threadId = ResponseIdFrom(body1);

        // 2º mensaje CON ese conversationId.
        var second = await client.PostAsJsonAsync("/api/conversations/messages",
            new { mensaje = "El recepcionista; monto, fecha y consecutivo", previousResponseId = threadId, conversationId });
        string body2 = await second.Content.ReadAsStringAsync();
        Assert.Equal(conversationId, ConversationIdFrom(body2));

        // GET /{id} devuelve 4 mensajes (2 user + 2 agent) y lastResponseId.
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/conversations/{conversationId}");
        Assert.Equal("Abierta", detail.GetProperty("status").GetString());
        var messages = detail.GetProperty("messages");
        Assert.Equal(4, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("agent", messages[1].GetProperty("role").GetString());
        Assert.False(string.IsNullOrEmpty(detail.GetProperty("lastResponseId").GetString()));

        // GET lista la incluye con preview.
        var list = await client.GetFromJsonAsync<JsonElement>("/api/conversations");
        JsonElement listed = list.EnumerateArray().First(c => c.GetProperty("id").GetString() == conversationId.ToString());
        Assert.StartsWith("Quiero controlar los pagos", listed.GetProperty("preview").GetString());

        // Completar con conversationId: sacar texto/area del draft del 2º turno.
        Match draftMatch = Regex.Match(body2, "\"requerimiento\":\\{\"texto\":\"([^\"]+)\",\"area\":\"([^\"]+)\"\\}");
        Assert.True(draftMatch.Success);
        string texto = draftMatch.Groups[1].Value;
        string area = draftMatch.Groups[2].Value;

        var complete = await client.PostAsJsonAsync("/api/conversations/complete", new { texto, area, conversationId });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = await complete.Content.ReadFromJsonAsync<JsonElement>();
        string analysisId = completed.GetProperty("analysisId").GetString()!;

        var afterComplete = await client.GetFromJsonAsync<JsonElement>($"/api/conversations/{conversationId}");
        Assert.Equal("Completada", afterComplete.GetProperty("status").GetString());
        Assert.Equal(analysisId, afterComplete.GetProperty("analysisId").GetString());
    }

    [Fact]
    public async Task GetById_Inexistente_Devuelve404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/conversations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
