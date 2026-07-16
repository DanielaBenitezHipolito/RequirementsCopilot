using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RequirementsCopilot.Tests.Api;

public class AnalysesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AnalysesEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static MultipartFormDataContent File(string name, string content)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content)), "file", name);
        return form;
    }

    private static string AnalysisIdFrom(string sseBody)
    {
        Match match = Regex.Match(sseBody, "\"analysisId\":\"([^\"]+)\"");
        Assert.True(match.Success, "El SSE no incluyó el analysisId en el evento done.");
        return match.Groups[1].Value;
    }

    [Fact]
    public async Task Post_ArchivoTxt_EvaluaPreguntaYPersisteSinHistorias()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/analyses", File("spec.txt", "El sistema debe registrar pagos."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("event: requirement", body);
        Assert.Contains("event: evaluation", body);
        Assert.Contains("event: clarification", body); // REQ-002 ambiguo pregunta
        Assert.DoesNotContain("event: story", body); // historias ya no son automáticas
        Assert.DoesNotContain("event: testcase", body);
        Assert.Contains("event: done", body);

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/analyses");
        Assert.NotEmpty(list!);
        Assert.Equal("Completed", list![0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task FlujoManual_ResponderYGenerarHistorias()
    {
        var client = _factory.CreateClient();
        var post = await client.PostAsync("/api/analyses", File("spec.txt", "doc"));
        string analysisId = AnalysisIdFrom(await post.Content.ReadAsStringAsync());

        // Aprobado directo: genera sin responder nada
        var direct = await client.PostAsync($"/api/analyses/{analysisId}/requirements/REQ-001/stories", null);
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        var directDto = await direct.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(directDto.GetProperty("historias").GetArrayLength() > 0);
        Assert.True(directDto.GetProperty("historias")[0].TryGetProperty("caso", out _));

        // Ambiguo: bloqueado hasta responder
        var blocked = await client.PostAsync($"/api/analyses/{analysisId}/requirements/REQ-002/stories", null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        var answers = await client.PutAsJsonAsync($"/api/analyses/{analysisId}/requirements/REQ-002/clarifications",
            new { respuestas = new[] { "Menos de 2 segundos", "Consultas y pagos" } });
        Assert.Equal(HttpStatusCode.OK, answers.StatusCode);
        var answeredDto = await answers.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(answeredDto.GetProperty("listoParaHistorias").GetBoolean());

        var generated = await client.PostAsync($"/api/analyses/{analysisId}/requirements/REQ-002/stories", null);
        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);

        // Doble generación: conflicto
        var again = await client.PostAsync($"/api/analyses/{analysisId}/requirements/REQ-002/stories", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task GenerateStories_AnalisisInexistente_Devuelve404()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/analyses/{Guid.NewGuid()}/requirements/REQ-001/stories", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ExtensionInvalida_Devuelve400ConMensaje()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/analyses", File("spec.exe", "x"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("mensaje", body);
    }

    [Fact]
    public async Task Post_SinArchivo_Devuelve400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/analyses", new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Inexistente_Devuelve404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/analyses/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
