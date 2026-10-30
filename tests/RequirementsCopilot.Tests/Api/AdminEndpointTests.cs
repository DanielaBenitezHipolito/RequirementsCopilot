using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RequirementsCopilot.Tests.Api;

public class AdminEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string JwtKey = "clave-de-pruebas-para-jwt-de-al-menos-32-chars!";

    private readonly WebApplicationFactory<Program> _factory;

    public AdminEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private WebApplicationFactory<Program> AuthFactory() => _factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Auth:JwtKey", JwtKey);
        builder.UseSetting("Auth:AdminEmail", "admin@test.local");
        builder.UseSetting("Auth:AdminPassword", "Admin#2026!");
    });

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Usage_Admin_DevuelveAgregados()
    {
        var client = AuthFactory().CreateClient();
        string token = await LoginAsync(client, "admin@test.local", "Admin#2026!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/usage?dias=30");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("llamadas").GetInt32()); // el Fake no registra uso
        Assert.Equal(JsonValueKind.Array, body.GetProperty("porAgente").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("porDia").ValueKind);
    }

    [Fact]
    public async Task Usage_ConPreciosConfigurados_IncluyeCostoEnUsd()
    {
        var factory = AuthFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Pricing:gpt-5-mini:InputPer1M", "0.25");
            builder.UseSetting("Pricing:gpt-5-mini:OutputPer1M", "2.00");
        });
        var client = factory.CreateClient();
        string token = await LoginAsync(client, "admin@test.local", "Admin#2026!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = await (await client.GetAsync("/api/admin/usage")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("preciosConfigurados").GetBoolean());
        Assert.Equal(0, body.GetProperty("costoUsd").GetDecimal()); // sin consumo registrado aún
    }

    [Fact]
    public async Task Usage_SinPrecios_CostoEsNull()
    {
        var client = AuthFactory().CreateClient();
        string token = await LoginAsync(client, "admin@test.local", "Admin#2026!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = await (await client.GetAsync("/api/admin/usage")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("preciosConfigurados").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("costoUsd").ValueKind);
    }

    [Fact]
    public async Task Usage_Analista_Devuelve403()
    {
        var client = AuthFactory().CreateClient();
        string adminToken = await LoginAsync(client, "admin@test.local", "Admin#2026!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await client.PostAsJsonAsync("/api/users",
            new { email = "ana2@test.local", nombre = "Ana", rol = "Analista", password = "Ana#2026!" });

        string anaToken = await LoginAsync(client, "ana2@test.local", "Ana#2026!");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/usage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", anaToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }
}
