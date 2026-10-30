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
    public async Task Post_ArchivoTxt_EvaluaPreguntaYPersisteSinCasoDeUso()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/analyses", File("spec.txt", "El sistema debe registrar pagos."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("event: requirement", body);
        Assert.Contains("event: evaluation", body);
        Assert.Contains("event: clarification", body); // REQ-002 ambiguo pregunta
        Assert.DoesNotContain("event: story", body); // el caso de uso ya no es automático
        Assert.DoesNotContain("event: testcase", body);
        Assert.Contains("event: summary", body); // resumen ejecutivo antes de done
        Assert.Contains("event: done", body);
        Assert.True(Regex.IsMatch(body, "event: summary.*?event: done", RegexOptions.Singleline));

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/analyses");
        Assert.NotEmpty(list!);
        Assert.Equal("Completed", list![0].GetProperty("status").GetString());

        string analysisId = AnalysisIdFrom(body);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        Assert.False(string.IsNullOrWhiteSpace(detail.GetProperty("resumen").GetString()));
    }

    [Fact]
    public async Task Reevaluate_RequerimientoAmbiguo_ResponderApruebaYPersiste()
    {
        var client = _factory.CreateClient();
        var post = await client.PostAsync("/api/analyses", File("spec.txt", "doc"));
        string analysisId = AnalysisIdFrom(await post.Content.ReadAsStringAsync());

        // REQ-002 es ambiguo en el Fake y trae 2 preguntas de clarificación.
        var reevaluate = await client.PostAsJsonAsync(
            $"/api/analyses/{analysisId}/requirements/REQ-002/reevaluate",
            new { respuestas = new[] { "Menos de 2 segundos", "Consultas y pagos" } });

        Assert.Equal(HttpStatusCode.OK, reevaluate.StatusCode);
        var dto = await reevaluate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(dto.GetProperty("evaluacion").GetProperty("pasa").GetBoolean());

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        var req002 = detail.GetProperty("requerimientos").EnumerateArray()
            .First(r => r.GetProperty("codigo").GetString() == "REQ-002");
        Assert.True(req002.GetProperty("evaluacion").GetProperty("pasa").GetBoolean());
        Assert.Equal("Menos de 2 segundos", req002.GetProperty("aclaraciones")[0].GetProperty("respuesta").GetString());
    }

    [Fact]
    public async Task Reevaluate_SinPreguntasPendientes_Devuelve409()
    {
        var client = _factory.CreateClient();
        var post = await client.PostAsync("/api/analyses", File("spec.txt", "doc"));
        string analysisId = AnalysisIdFrom(await post.Content.ReadAsStringAsync());

        // REQ-001 está aprobado y sin preguntas de clarificación: no hay nada que re-evaluar.
        var response = await client.PostAsJsonAsync(
            $"/api/analyses/{analysisId}/requirements/REQ-001/reevaluate",
            new { respuestas = new[] { "no aplica" } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reevaluate_PreguntasPendientesSinRespuestas_Devuelve400()
    {
        var client = _factory.CreateClient();
        var post = await client.PostAsync("/api/analyses", File("spec.txt", "doc"));
        string analysisId = AnalysisIdFrom(await post.Content.ReadAsStringAsync());

        // REQ-002 es ambiguo y tiene preguntas pendientes, pero no se envían respuestas.
        var response = await client.PostAsJsonAsync(
            $"/api/analyses/{analysisId}/requirements/REQ-002/reevaluate",
            new { respuestas = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FlujoManual_ResponderYGenerarCasoDeUso()
    {
        var client = _factory.CreateClient();
        var post = await client.PostAsync("/api/analyses", File("spec.txt", "doc"));
        string analysisId = AnalysisIdFrom(await post.Content.ReadAsStringAsync());

        // Aprobado directo: genera sin responder nada
        var direct = await client.PostAsync($"/api/analyses/{analysisId}/requirements/REQ-001/stories", null);
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        var directDto = await direct.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(directDto.TryGetProperty("caso", out var caso));
        Assert.False(string.IsNullOrWhiteSpace(caso.GetProperty("nombre").GetString()));

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

    private async Task<string> AnalyzeAndGenerateUseCaseAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/analyses", File("spec.txt", "El sistema debe registrar pagos."));
        string analysisId = AnalysisIdFrom(await response.Content.ReadAsStringAsync());
        var stories = await client.PostAsync($"/api/analyses/{analysisId}/requirements/REQ-001/stories", null);
        Assert.Equal(HttpStatusCode.OK, stories.StatusCode);
        return analysisId;
    }

    [Fact]
    public async Task GenerateStories_YaGenerado_409PeroRegenerarLoReemplaza()
    {
        var client = _factory.CreateClient();
        string analysisId = await AnalyzeAndGenerateUseCaseAsync(client);

        var repeat = await client.PostAsync($"/api/analyses/{analysisId}/requirements/REQ-001/stories", null);
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);

        var regenerate = await client.PostAsync(
            $"/api/analyses/{analysisId}/requirements/REQ-001/stories?regenerar=true", null);
        Assert.Equal(HttpStatusCode.OK, regenerate.StatusCode);
        var detail = await regenerate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(detail.GetProperty("caso").GetProperty("nombre").GetString()));
    }

    [Fact]
    public async Task EditUseCase_ReemplazaElCasoDeUso()
    {
        var client = _factory.CreateClient();
        string analysisId = await AnalyzeAndGenerateUseCaseAsync(client);

        var edit = await client.PutAsJsonAsync($"/api/analyses/{analysisId}/requirements/REQ-001/use-case", new
        {
            nombre = "Caso editado a mano",
            objetivo = "Objetivo corregido",
            descripcion = "Descripción corregida",
            actores = new[] { new { nombre = "Cajero", descripcion = "Cobra" } },
            precondiciones = new[] { "Caja abierta" },
            trigger = "El cliente paga",
            flujos = new[] { new { titulo = "Principal", pasos = new[] { new { numero = 1, accion = "Cobrar", resultadoEsperado = "Pagado" } } } },
            extensiones = new string[0],
            frecuencia = "Diaria",
            importancia = "Alta",
            urgencia = "Alta",
            comentarios = new string[0],
        });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        var caso = detail.GetProperty("requerimientos")[0].GetProperty("caso");
        Assert.Equal("Caso editado a mano", caso.GetProperty("nombre").GetString());
        Assert.Equal("Cajero", caso.GetProperty("actores")[0].GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task EditUseCase_SinObjetivo_Devuelve400()
    {
        var client = _factory.CreateClient();
        string analysisId = await AnalyzeAndGenerateUseCaseAsync(client);
        var edit = await client.PutAsJsonAsync($"/api/analyses/{analysisId}/requirements/REQ-001/use-case",
            new { nombre = "X", objetivo = "" });
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
    }

    [Fact]
    public async Task GenerateUserStories_ConRegenerar_Reemplaza()
    {
        var client = _factory.CreateClient();
        string analysisId = await AnalyzeAndGenerateUseCaseAsync(client);
        await client.PostAsync($"/api/analyses/{analysisId}/requirements/REQ-001/user-stories", null);

        var repeat = await client.PostAsync($"/api/analyses/{analysisId}/requirements/REQ-001/user-stories", null);
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        var regenerate = await client.PostAsync(
            $"/api/analyses/{analysisId}/requirements/REQ-001/user-stories?regenerar=true", null);
        Assert.Equal(HttpStatusCode.OK, regenerate.StatusCode);
    }

    [Fact]
    public async Task Post_AclaracionesConOpciones_LlegaronAlDetalle()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/analyses", File("spec.txt", "doc"));
        string analysisId = AnalysisIdFrom(await response.Content.ReadAsStringAsync());

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        var req002 = detail.GetProperty("requerimientos").EnumerateArray()
            .First(r => r.GetProperty("codigo").GetString() == "REQ-002");
        var primera = req002.GetProperty("aclaraciones")[0];
        Assert.True(primera.GetProperty("opciones").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task SetStage_CambiaLaEtapaYSePersiste()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/analyses", File("spec.txt", "El sistema debe registrar pagos."));
        string analysisId = AnalysisIdFrom(await response.Content.ReadAsStringAsync());

        // REQ-001 aprueba con el Fake → etapa Approved automática.
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        Assert.Equal("Approved", detail.GetProperty("requerimientos")[0].GetProperty("etapa").GetString());

        var put = await client.PutAsJsonAsync($"/api/analyses/{analysisId}/requirements/REQ-001/stage",
            new { etapa = "InDevelopment" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        detail = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        Assert.Equal("InDevelopment", detail.GetProperty("requerimientos")[0].GetProperty("etapa").GetString());

        var invalid = await client.PutAsJsonAsync($"/api/analyses/{analysisId}/requirements/REQ-001/stage",
            new { etapa = "Inexistente" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task References_AdjuntarListarYEliminar()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/analyses", File("spec.txt", "doc"));
        string analysisId = AnalysisIdFrom(await response.Content.ReadAsStringAsync());

        var form = File("pdf-modelo.txt", "Estimado cliente: adjunto el formulario diligenciado.");
        var attach = await client.PostAsync($"/api/analyses/{analysisId}/requirements/REQ-001/references", form);
        Assert.Equal(HttpStatusCode.OK, attach.StatusCode);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        var req = detail.GetProperty("requerimientos").EnumerateArray()
            .First(r => r.GetProperty("codigo").GetString() == "REQ-001");
        var referencia = Assert.Single(req.GetProperty("referencias").EnumerateArray().ToArray());
        Assert.Equal("pdf-modelo.txt", referencia.GetProperty("nombre").GetString());
        Assert.True(referencia.GetProperty("caracteres").GetInt32() > 0);

        var delete = await client.DeleteAsync(
            $"/api/analyses/{analysisId}/requirements/REQ-001/references/pdf-modelo.txt");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(
            $"/api/analyses/{analysisId}/requirements/REQ-001/references/pdf-modelo.txt")).StatusCode);
    }

    [Fact]
    public async Task References_FormatoImagen_Devuelve400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/analyses", File("spec.txt", "doc"));
        string analysisId = AnalysisIdFrom(await response.Content.ReadAsStringAsync());
        var attach = await client.PostAsync(
            $"/api/analyses/{analysisId}/requirements/REQ-001/references", File("foto.png", "bytes"));
        Assert.Equal(HttpStatusCode.BadRequest, attach.StatusCode);
    }

    [Fact]
    public async Task AssignProject_ReasignaYQuitaProyecto()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/analyses", File("spec.txt", "doc"));
        string analysisId = AnalysisIdFrom(await response.Content.ReadAsStringAsync());

        // Asignar un análisis viejo (sin proyecto) a un proyecto.
        var assign = await client.PutAsJsonAsync($"/api/analyses/{analysisId}/project", new { proyecto = "Hoteleria" });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        Assert.Equal("Hoteleria", detail.GetProperty("proyecto").GetString());

        // Vacío = quitarle el proyecto.
        await client.PutAsJsonAsync($"/api/analyses/{analysisId}/project", new { proyecto = "" });
        detail = await client.GetFromJsonAsync<JsonElement>($"/api/analyses/{analysisId}");
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("proyecto").ValueKind);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"/api/analyses/{Guid.NewGuid()}/project", new { proyecto = "X" })).StatusCode);
    }

    [Fact]
    public async Task Delete_EliminaElAnalisisYSuContexto()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/analyses", File("spec.txt", "El sistema debe registrar pagos."));
        string analysisId = AnalysisIdFrom(await response.Content.ReadAsStringAsync());

        var delete = await client.DeleteAsync($"/api/analyses/{analysisId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/analyses/{analysisId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/analyses/{analysisId}")).StatusCode);
    }

    [Fact]
    public async Task GenerateStories_AnalisisInexistente_Devuelve404()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/analyses/{Guid.NewGuid()}/requirements/REQ-001/stories", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ArchivoPng_SeAnalizaComoImagen()
    {
        var client = _factory.CreateClient();
        var form = new MultipartFormDataContent();
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        form.Add(new ByteArrayContent(png), "file", "mockup.png");

        var response = await client.PostAsync("/api/analyses", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("event: requirement", body);
        Assert.Contains("event: done", body);
    }

    [Fact]
    public async Task ExtractText_ArchivoPng_DevuelveConteoDeImagenes()
    {
        var client = _factory.CreateClient();
        var form = new MultipartFormDataContent();
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        form.Add(new ByteArrayContent(png), "file", "mockup.png");

        var response = await client.PostAsync("/api/documents/extract", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(string.Empty, payload.GetProperty("texto").GetString());
        Assert.Equal(1, payload.GetProperty("imagenes").GetInt32());
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
