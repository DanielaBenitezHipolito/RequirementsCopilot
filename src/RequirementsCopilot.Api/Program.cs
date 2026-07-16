using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Infrastructure.Chat;
using RequirementsCopilot.Infrastructure.Documents;
using RequirementsCopilot.Infrastructure.Mongo;
using RequirementsCopilot.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string corsOrigin = builder.Configuration["Cors:Origin"] ?? "http://localhost:5173";
// En dev Vite salta de puerto (5173 ocupado -> 5174/5175): se acepta cualquier loopback además del origen configurado.
bool allowLoopback = builder.Environment.IsDevelopment();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.SetIsOriginAllowed(origin =>
            origin == corsOrigin ||
            (allowLoopback && Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback))
        .AllowAnyHeader().AllowAnyMethod()));

// Puertos y adaptadores seleccionables por configuración (patrón Providers de JYDE).
string chatProvider = builder.Configuration["Providers:Chat"] ?? "Fake";
if (chatProvider == "Foundry")
{
    var foundryOptions = builder.Configuration.GetSection("Foundry").Get<FoundryOptions>() ?? new FoundryOptions();
    builder.Services.AddSingleton(foundryOptions);
    builder.Services.AddHttpClient<FoundryChatCompletion>();
    builder.Services.AddScoped<IChatCompletion>(sp => sp.GetRequiredService<FoundryChatCompletion>());
}
else
{
    builder.Services.AddSingleton<IChatCompletion, FakeChatCompletion>();
}

string repositoryProvider = builder.Configuration["Providers:AnalysisRepository"] ?? "InMemory";
if (repositoryProvider == "Mongo")
{
    var mongoOptions = builder.Configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions();
    builder.Services.AddSingleton(sp =>
        new MongoDB.Driver.MongoClient(mongoOptions.ConnectionString).GetDatabase(mongoOptions.Database));
    builder.Services.AddSingleton<IAnalysisRepository, MongoAnalysisRepository>();
}
else
{
    builder.Services.AddSingleton<IAnalysisRepository, InMemoryAnalysisRepository>();
}

builder.Services.AddSingleton(new AnalysisOptions
{
    PassThreshold = builder.Configuration.GetValue("Analysis:PassThreshold", 3.5),
});
builder.Services.AddSingleton<IDocumentTextExtractor, CompositeTextExtractor>();
builder.Services.AddScoped<RequirementExtractorAgent>();
builder.Services.AddScoped<RequirementEvaluatorAgent>();
builder.Services.AddScoped<ClarifierAgent>();
builder.Services.AddScoped<StoryWriterAgent>();
builder.Services.AddScoped<TestCaseWriterAgent>();
builder.Services.AddScoped<AnalysisOrchestrator>();
builder.Services.AddScoped<AnalysisQueries>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.Run();

public partial class Program;
