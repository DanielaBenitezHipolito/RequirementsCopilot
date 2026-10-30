using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using RequirementsCopilot.Api.Auth;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Usage;
using RequirementsCopilot.Application.Users;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Application.Conversations;
using RequirementsCopilot.Application.Projects;
using RequirementsCopilot.Infrastructure.Chat;
using RequirementsCopilot.Infrastructure.Documents;
using RequirementsCopilot.Infrastructure.Mongo;
using RequirementsCopilot.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Hosts como Render/Railway asignan el puerto por la variable PORT; Kestrel escucha ahí.
string? port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cors:Origin admite lista separada por ';' (Vite salta de puerto: 5173 ocupado -> 5174/5175).
string[] corsOrigins = (builder.Configuration["Cors:Origin"] ?? "http://localhost:5173")
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
bool allowLoopback = builder.Environment.IsDevelopment();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.SetIsOriginAllowed(origin =>
            corsOrigins.Contains(origin) ||
            (allowLoopback && Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback))
        .AllowAnyHeader().AllowAnyMethod()));

// Puertos y adaptadores seleccionables por configuración (patrón Providers de JYDE).
string chatProvider = builder.Configuration["Providers:Chat"] ?? "Fake";
if (chatProvider == "Foundry")
{
    var foundryOptions = builder.Configuration.GetSection("Foundry").Get<FoundryOptions>() ?? new FoundryOptions();
    builder.Services.AddSingleton(foundryOptions);
    // Prompts versionados en el repo (ADR-0008): se copian junto al binario y se cargan al arrancar,
    // así un prompt faltante tumba el arranque y no una llamada en producción.
    builder.Services.AddSingleton(new FilePromptCatalog(Path.Combine(AppContext.BaseDirectory, "prompts")));
    builder.Services.AddHttpClient<FoundryChatCompletion>();
    builder.Services.AddScoped<IChatCompletion>(sp => sp.GetRequiredService<FoundryChatCompletion>());
}
else
{
    builder.Services.AddSingleton<IChatCompletion, FakeChatCompletion>();
}

// Este switch gobierna ambas persistencias (análisis y conversaciones): mismo backing store.
string repositoryProvider = builder.Configuration["Providers:AnalysisRepository"] ?? "InMemory";
if (repositoryProvider == "Mongo")
{
    // MongoDB.Driver 3.x no serializa Guid sin representación explícita. El atributo
    // [BsonGuidRepresentation] no aplica a Guid? (AnalysisId), así que se registra global.
    try
    {
        MongoDB.Bson.Serialization.BsonSerializer.RegisterSerializer(
            new MongoDB.Bson.Serialization.Serializers.GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard));
    }
    catch (MongoDB.Bson.BsonSerializationException)
    {
        // Ya registrado (reinicios in-process, p. ej. tests): ignorar.
    }

    var mongoOptions = builder.Configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions();
    builder.Services.AddSingleton(sp =>
        new MongoDB.Driver.MongoClient(mongoOptions.ConnectionString).GetDatabase(mongoOptions.Database));
    builder.Services.AddSingleton<IAnalysisRepository, MongoAnalysisRepository>();
    builder.Services.AddSingleton<IConversationRepository, MongoConversationRepository>();
    builder.Services.AddSingleton<IProjectRepository, MongoProjectRepository>();
    builder.Services.AddSingleton<IUserRepository, MongoUserRepository>();
    builder.Services.AddSingleton<ILlmUsageRepository, MongoLlmUsageRepository>();
}
else
{
    builder.Services.AddSingleton<IAnalysisRepository, InMemoryAnalysisRepository>();
    builder.Services.AddSingleton<IConversationRepository, InMemoryConversationRepository>();
    builder.Services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();
    builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
    builder.Services.AddSingleton<ILlmUsageRepository, InMemoryLlmUsageRepository>();
}

// Autenticación (ADR-0010): se habilita configurando Auth__JwtKey (≥ 32 chars). Sin clave la API
// queda abierta (demo local / pruebas), y los endpoints [Authorize] responden 401.
AuthOptions authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        // Con auth deshabilitado se firma contra una clave aleatoria: ningún token externo valida.
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            authOptions.Enabled ? authOptions.JwtKey : Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"))),
        ClockSkew = TimeSpan.FromMinutes(1),
    });
builder.Services.AddAuthorization(options =>
{
    if (authOptions.Enabled)
    {
        // Todo endpoint exige usuario autenticado salvo [AllowAnonymous] (login, status, health).
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }
});

builder.Services.AddSingleton(
    builder.Configuration.GetSection("Analysis").Get<AnalysisOptions>() ?? new AnalysisOptions());
// Precios contratados por modelo (USD/1M tokens) para el tablero; sin configurar = solo tokens.
builder.Services.AddSingleton<IReadOnlyDictionary<string, ModelPrice>>(
    new Dictionary<string, ModelPrice>(
        builder.Configuration.GetSection("Pricing").Get<Dictionary<string, ModelPrice>>()
            ?? new Dictionary<string, ModelPrice>(),
        StringComparer.OrdinalIgnoreCase));
builder.Services.AddSingleton<IDocumentTextExtractor, CompositeTextExtractor>();
builder.Services.AddScoped<RequirementExtractorAgent>();
builder.Services.AddScoped<OverlapCheckerAgent>();
builder.Services.AddScoped<RequirementEvaluatorAgent>();
builder.Services.AddScoped<ClarifierAgent>();
builder.Services.AddScoped<UseCaseWriterAgent>();
builder.Services.AddScoped<UserStoryWriterAgent>();
builder.Services.AddScoped<RequirementBuilderAgent>();
builder.Services.AddScoped<ExecutiveSummaryAgent>();
builder.Services.AddScoped<ProjectContextLoader>();
builder.Services.AddScoped<AnalysisOrchestrator>();
builder.Services.AddScoped<AnalysisQueries>();

var app = builder.Build();

// Siembra del admin inicial: solo con auth habilitado y el repositorio de usuarios vacío.
if (authOptions.Enabled)
{
    using IServiceScope scope = app.Services.CreateScope();
    IUserRepository users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    if ((await users.GetAllAsync()).Count == 0)
    {
        if (string.IsNullOrWhiteSpace(authOptions.AdminEmail) || string.IsNullOrWhiteSpace(authOptions.AdminPassword))
        {
            app.Logger.LogWarning(
                "Auth habilitado sin usuarios y sin Auth__AdminEmail/Auth__AdminPassword: nadie podrá iniciar sesión.");
        }
        else
        {
            await users.SaveAsync(UserAccount.Create(authOptions.AdminEmail, "Administrador",
                UserAccount.AdminRole, PasswordHasher.Hash(authOptions.AdminPassword)));
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sirve el SPA compilado (wwwroot) si está presente (despliegue monolito: front + API juntos).
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// Rutas que no son /api/* caen al index del SPA (navegación cliente); nunca colisionan con la API.
// AllowAnonymous: el cascarón del SPA es público (la pantalla de login vive ahí); los datos están
// detrás de la API autenticada. Sin esto, la FallbackPolicy devuelve 401 en la raíz del sitio.
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program;
