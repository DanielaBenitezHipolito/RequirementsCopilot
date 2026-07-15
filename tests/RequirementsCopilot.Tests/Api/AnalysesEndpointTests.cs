using System.Net;
using System.Net.Http.Json;
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

    [Fact]
    public async Task Post_ArchivoTxt_EmiteSseYPersiste()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/analyses", File("spec.txt", "El sistema debe registrar pagos."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("event: requirement", body);
        Assert.Contains("event: evaluation", body);
        Assert.Contains("event: story", body);
        Assert.Contains("event: testcase", body);
        Assert.Contains("event: done", body);

        var list = await client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/analyses");
        Assert.NotEmpty(list!);
        Assert.Equal("Completed", list![0].GetProperty("status").GetString());
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
