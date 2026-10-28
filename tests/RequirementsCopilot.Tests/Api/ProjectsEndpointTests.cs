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

    [Fact]
    public async Task Upload_ExtensionInvalida_Devuelve400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/projects", MdForm("spec.txt", "contenido"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_MdVacio_CreaProyectoSinContenido()
    {
        // ADR-0009: un proyecto nuevo puede arrancar vacío; su contexto se construye con los casos de uso.
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/projects", MdForm("vacio.md", "   ", nombre: "Vacio"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/projects/Vacio");
        Assert.Equal(string.Empty, detail.GetProperty("contenido").GetString());
    }

    [Fact]
    public async Task Upload_SinArchivoNiNombre_Devuelve400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/projects", new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_SoloNombre_CreaProyectoVacio()
    {
        // Proyecto nuevo (ADR-0009): se crea sin .md; el contexto se construye con sus casos de uso.
        var client = _factory.CreateClient();
        var form = new MultipartFormDataContent { { new StringContent("Proyecto Nuevo"), "nombre" } };

        var response = await client.PostAsync("/api/projects", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/projects/Proyecto Nuevo");
        Assert.Equal(string.Empty, detail.GetProperty("contenido").GetString());
    }

    private static MultipartFormDataContent TxtForm(string fileName, string content)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName);
        return form;
    }

    [Fact]
    public async Task AgregarDocumento_AnexaElTextoAlContenido()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/api/projects", MdForm("base.md", "Descripción base.", nombre: "ConDocs"));

        var response = await client.PostAsync("/api/projects/ConDocs/documents",
            TxtForm("casos-existentes.txt", "CU-01: El usuario inicia sesión."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/projects/ConDocs");
        string contenido = detail.GetProperty("contenido").GetString()!;
        Assert.Contains("Descripción base.", contenido);
        Assert.Contains("casos-existentes.txt", contenido);
        Assert.Contains("CU-01: El usuario inicia sesión.", contenido);
    }

    [Fact]
    public async Task AgregarDocumento_ProyectoInexistente_Devuelve404()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/projects/NoExiste/documents", TxtForm("x.txt", "contenido"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AgregarDocumento_Imagen_Devuelve400()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/api/projects", MdForm("p.md", "x", nombre: "SoloTexto"));
        var response = await client.PostAsync("/api/projects/SoloTexto/documents", TxtForm("foto.png", "bytes"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetByName_IncluyeConteoDeCasosDeUsoAsociados()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/api/projects", MdForm("ctx.md", "Sistema base.", nombre: "CtxProj"));

        // Análisis del proyecto → REQ-001 queda aprobado (Fake) → generar su caso de uso.
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("El sistema debe registrar pagos.")), "file", "spec.txt");
        form.Add(new StringContent("CtxProj"), "proyecto");
        var analyze = await client.PostAsync("/api/analyses", form);
        string body = await analyze.Content.ReadAsStringAsync();
        string analysisId = System.Text.RegularExpressions.Regex.Match(body, "\"analysisId\":\"([^\"]+)\"").Groups[1].Value;
        var stories = await client.PostAsync($"/api/analyses/{analysisId}/requirements/REQ-001/stories", null);
        Assert.Equal(HttpStatusCode.OK, stories.StatusCode);

        var detail = await client.GetFromJsonAsync<JsonElement>("/api/projects/CtxProj");
        Assert.Equal(1, detail.GetProperty("casosDeUso").GetInt32());
    }

    [Fact]
    public async Task Update_YDelete_Proyecto()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/api/projects", MdForm("Siniestros.md", "v1"));

        var update = await client.PutAsJsonAsync("/api/projects/Siniestros", new { contenido = "v2 editada" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/projects/Siniestros");
        Assert.Equal("v2 editada", detail.GetProperty("contenido").GetString());

        // Vaciar el contenido es válido (ADR-0009): el proyecto queda solo con sus casos de uso.
        var empty = await client.PutAsJsonAsync("/api/projects/Siniestros", new { contenido = " " });
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        var missing = await client.PutAsJsonAsync("/api/projects/NoExiste", new { contenido = "x" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var delete = await client.DeleteAsync("/api/projects/Siniestros");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/projects/Siniestros")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/projects/Siniestros")).StatusCode);
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
