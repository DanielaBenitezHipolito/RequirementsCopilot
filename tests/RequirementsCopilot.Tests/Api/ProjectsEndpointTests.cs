using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RequirementsCopilot.Tests.Api;

public class ProjectsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProjectsEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static MultipartFormDataContent MdForm(string fileName, string content, string? nombre = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/markdown");
        form.Add(file, "file", fileName);
        if (nombre is not null)
        {
            form.Add(new StringContent(nombre), "nombre");
        }
        return form;
    }

    [Fact]
    public async Task Upload_Listar_YObtener_ProyectoMd()
    {
        var client = _factory.CreateClient();

        var upload = await client.PostAsync("/api/projects", MdForm("Hoteleria.md", "# Sistema hotelero\nModulo de pagos existente."));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var uploaded = await upload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hoteleria", uploaded.GetProperty("nombre").GetString());

        var list = await client.GetFromJsonAsync<JsonElement>("/api/projects");
        Assert.Contains(list.EnumerateArray(), p => p.GetProperty("nombre").GetString() == "Hoteleria");

        var detail = await client.GetFromJsonAsync<JsonElement>("/api/projects/hoteleria");
        Assert.Contains("Modulo de pagos existente.", detail.GetProperty("contenido").GetString());

        var missing = await client.GetAsync("/api/projects/no-existe");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Upload_NombreExplicito_ReemplazaContenido()
    {
        var client = _factory.CreateClient();

        await client.PostAsync("/api/projects", MdForm("v1.md", "version 1", nombre: "Polizas"));
        await client.PostAsync("/api/projects", MdForm("v2.md", "version 2", nombre: "Polizas"));

        var detail = await client.GetFromJsonAsync<JsonElement>("/api/projects/Polizas");
        Assert.Equal("version 2", detail.GetProperty("contenido").GetString());
    }

    [Theory]
    [InlineData("spec.txt", "contenido")]
    [InlineData("vacio.md", "   ")]
    public async Task Upload_Invalido_Devuelve400(string fileName, string content)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/projects", MdForm(fileName, content));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_SinArchivo_Devuelve400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/projects", new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Conversacion_ConProyecto_PersisteYExponeElProyecto()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/api/projects", MdForm("Reservas.md", "# Reservas\nYa existe check-in."));

        var first = await client.PostAsJsonAsync("/api/conversations/messages",
            new { mensaje = "Quiero controlar los pagos de las reservas", proyecto = "Reservas" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        string body = await first.Content.ReadAsStringAsync();
        string conversationId = System.Text.RegularExpressions.Regex
            .Match(body, "\"conversationId\":\"([^\"]+)\"").Groups[1].Value;

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/conversations/{conversationId}");
        Assert.Equal("Reservas", detail.GetProperty("proyecto").GetString());

        var complete = await client.PostAsJsonAsync("/api/conversations/complete",
            new { texto = "El sistema debe registrar pagos con consecutivo", area = "Pagos", conversationId, proyecto = "Reservas" });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        string analysisId = (await complete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("analysisId").GetString()!;

        var analysis = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        Assert.Equal("Reservas", analysis.GetProperty("proyecto").GetString());
    }
}
