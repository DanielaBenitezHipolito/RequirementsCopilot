using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace RequirementsCopilot.Tests.Api;

public class AuthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string JwtKey = "clave-de-pruebas-para-jwt-de-al-menos-32-chars!";
    private const string AdminEmail = "admin@test.local";
    private const string AdminPassword = "Admin#2026!";

    private readonly WebApplicationFactory<Program> _factory;

    public AuthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    /// <summary>Factory con autenticación habilitada (JwtKey + admin sembrado).</summary>
    private WebApplicationFactory<Program> AuthFactory() => _factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Auth:JwtKey", JwtKey);
        builder.UseSetting("Auth:AdminEmail", AdminEmail);
        builder.UseSetting("Auth:AdminPassword", AdminPassword);
    });

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Status_SinJwtKey_ReportaDeshabilitado()
    {
        var client = _factory.CreateClient();
        var status = await client.GetFromJsonAsync<JsonElement>("/api/auth/status");
        Assert.False(status.GetProperty("habilitado").GetBoolean());
    }

    [Fact]
    public async Task Status_ConJwtKey_ReportaHabilitadoSinToken()
    {
        var client = AuthFactory().CreateClient();
        var response = await client.GetAsync("/api/auth/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(status.GetProperty("habilitado").GetBoolean());
    }

    [Fact]
    public async Task Login_AdminSembrado_DevuelveTokenYRol()
    {
        var client = AuthFactory().CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = AdminEmail, password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        Assert.Equal("Admin", body.GetProperty("rol").GetString());
    }

    [Fact]
    public async Task Login_PasswordIncorrecta_Devuelve401()
    {
        var client = AuthFactory().CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = AdminEmail, password = "otra" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EndpointProtegido_SinToken_401_ConToken_200()
    {
        var client = AuthFactory().CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/analyses")).StatusCode);

        string token = await LoginAsync(client, AdminEmail, AdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/analyses")).StatusCode);
    }

    [Fact]
    public async Task Health_SinToken_Responde200()
    {
        var client = AuthFactory().CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public async Task Users_CrudSoloParaAdmin()
    {
        var client = AuthFactory().CreateClient();
        string adminToken = await LoginAsync(client, AdminEmail, AdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Admin crea una analista.
        var create = await client.PostAsJsonAsync("/api/users",
            new { email = "ana@test.local", nombre = "Ana", rol = "Analista", password = "Ana#2026!" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/users");
        Assert.Contains(list.EnumerateArray(), u => u.GetProperty("email").GetString() == "ana@test.local");

        // La analista entra pero no administra usuarios.
        var anaClient = AuthFactory().CreateClient();
        // mismo backing store del factory original: ambos clients comparten InMemory del mismo host
        string anaToken = await LoginAsync(client, "ana@test.local", "Ana#2026!");
        var anaRequest = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        anaRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", anaToken);
        var forbidden = await client.SendAsync(anaRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        anaClient.Dispose();

        // La analista sí usa la API normal.
        var anaAnalyses = new HttpRequestMessage(HttpMethod.Get, "/api/analyses");
        anaAnalyses.Headers.Authorization = new AuthenticationHeaderValue("Bearer", anaToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(anaAnalyses)).StatusCode);

        // Eliminar usuaria.
        var delete = await client.DeleteAsync("/api/users/ana@test.local");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Users_NoPuedeEliminarseASiMismo()
    {
        var client = AuthFactory().CreateClient();
        string token = await LoginAsync(client, AdminEmail, AdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.DeleteAsync($"/api/users/{AdminEmail}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
