# RequirementsCopilot — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Analizador de requerimientos con IA: sube documento → pipeline de 4 agentes (extraer → evaluar rúbrica → historias → casos de prueba) → SSE en vivo → persiste en Mongo → frontend React.

**Architecture:** Hexagonal (Domain ← Application ← Infrastructure ← Api; Api NO referencia Domain). Pipeline secuencial de agentes tras el puerto `IChatCompletion` con adaptadores `Fake` (dev, $0) y `Foundry` (GPT-4.1-mini) por `Providers:Chat`. Spec: `docs/superpowers/specs/2026-07-14-requirements-copilot-design.md`.

**Tech Stack:** .NET 8, xUnit, MongoDB.Driver, PdfPig, DocumentFormat.OpenXml, Vite + React 19 + TypeScript + Zustand 5 + Tailwind 4 + Vitest.

## Global Constraints

- Raíz del repo: `D:\Repositories\Daniela Benitez\RequirementsCopilot` (git ya inicializado, rama `main`). Todos los paths de este plan son relativos a esa raíz.
- `net8.0`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`. SIN `GenerateDocumentationFile`.
- Namespaces raíz: `RequirementsCopilot.Domain|Application|Infrastructure|Api`.
- Regla de dependencias: Domain→(nada); Application→Domain; Infrastructure→Application; Api→Application+Infrastructure. **Api nunca referencia Domain.**
- Errores de API: `{ "mensaje": "..." }`, mensajes en español. UI en español.
- Config: `Providers:Chat` = `Fake` (default) | `Foundry`; `Analysis:PassThreshold` default `3.5`; `Foundry:Endpoint|ApiKey|Deployment`; `Mongo:ConnectionString|Database`. Secrets nunca al repo.
- Upload: extensiones `.pdf .docx .txt .md`, máximo 10 MB.
- Contratos JSON de agentes (campos en español, exactos):
  - extractor: `{"requerimientos":[{"codigo":"REQ-001","texto":"...","area":"..."}]}`
  - evaluador: `{"criterios":[{"nombre":"Claridad","score":4,"observacion":"..."}]}` — nombres: Claridad, Completitud, Verificabilidad, Consistencia, Factibilidad
  - historias: `{"historias":[{"rol":"...","quiero":"...","para":"...","criteriosAceptacion":["..."]}]}`
  - caso: `{"titulo":"...","precondiciones":["..."],"pasos":["..."],"resultadoEsperado":"..."}`
- Commits frecuentes, mensajes `feat:|test:|docs:|chore:` en español. NO commitear si tests fallan.

---

### Task 1: Scaffold de la solución

**Files:**
- Create: `.gitignore`, `Directory.Build.props`, `RequirementsCopilot.sln`, proyectos en `src/` y `tests/`

**Interfaces:**
- Produces: solución compilable con 5 proyectos y referencias correctas.

- [ ] **Step 1: Crear proyectos y solución**

```powershell
cd "D:\Repositories\Daniela Benitez\RequirementsCopilot"
dotnet new gitignore
dotnet new sln -n RequirementsCopilot
dotnet new classlib -n RequirementsCopilot.Domain -o src/RequirementsCopilot.Domain -f net8.0
dotnet new classlib -n RequirementsCopilot.Application -o src/RequirementsCopilot.Application -f net8.0
dotnet new classlib -n RequirementsCopilot.Infrastructure -o src/RequirementsCopilot.Infrastructure -f net8.0
dotnet new webapi -n RequirementsCopilot.Api -o src/RequirementsCopilot.Api -f net8.0 --use-controllers
dotnet new xunit -n RequirementsCopilot.Tests -o tests/RequirementsCopilot.Tests -f net8.0
dotnet sln add src/RequirementsCopilot.Domain src/RequirementsCopilot.Application src/RequirementsCopilot.Infrastructure src/RequirementsCopilot.Api tests/RequirementsCopilot.Tests
dotnet add src/RequirementsCopilot.Application reference src/RequirementsCopilot.Domain
dotnet add src/RequirementsCopilot.Infrastructure reference src/RequirementsCopilot.Application
dotnet add src/RequirementsCopilot.Api reference src/RequirementsCopilot.Application src/RequirementsCopilot.Infrastructure
dotnet add tests/RequirementsCopilot.Tests reference src/RequirementsCopilot.Domain src/RequirementsCopilot.Application src/RequirementsCopilot.Infrastructure
```

Borrar plantillas: `src/*/Class1.cs`, `tests/RequirementsCopilot.Tests/UnitTest1.cs`, `src/RequirementsCopilot.Api/WeatherForecast.cs` y `src/RequirementsCopilot.Api/Controllers/WeatherForecastController.cs` (si existen).

- [ ] **Step 2: Crear `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Company>Daniela Benitez</Company>
    <Product>RequirementsCopilot</Product>
    <NeutralLanguage>es-CO</NeutralLanguage>
  </PropertyGroup>
</Project>
```

Nota: los `.csproj` generados ya traen `<TargetFramework>`; dejarlos, no estorban.

- [ ] **Step 3: Compilar y verificar**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s). 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "chore: scaffold solución hexagonal .NET 8"
```

---

### Task 2: Dominio

**Files:**
- Create: `src/RequirementsCopilot.Domain/Analyses/CriterionScore.cs`, `Evaluation.cs`, `TestCase.cs`, `UserStory.cs`, `Requirement.cs`, `Analysis.cs`, `AnalysisStatus.cs`
- Test: `tests/RequirementsCopilot.Tests/Domain/EvaluationTests.cs`, `AnalysisTests.cs`

**Interfaces:**
- Produces (namespace `RequirementsCopilot.Domain.Analyses`):
  - `CriterionScore.Create(string criterion, int score, string observation)` — valida criterio no vacío y score 1..5.
  - `Evaluation.Create(IReadOnlyList<CriterionScore> scores, double threshold)` → props `Scores`, `Average` (double), `Passed` (bool: `Average >= threshold`), `Threshold`.
  - `TestCase.Create(string title, IReadOnlyList<string> preconditions, IReadOnlyList<string> steps, string expectedResult)`.
  - `UserStory.Create(string role, string goal, string benefit, IReadOnlyList<string> acceptanceCriteria)` + `AttachTestCase(TestCase)` → prop `TestCase` (`TestCase?`).
  - `Requirement.Create(string code, string text, string area)` + `Evaluate(Evaluation)` + `AddStory(UserStory)` → props `Code`, `Text`, `Area`, `Evaluation?`, `Stories` (IReadOnlyList).
  - `Analysis.Create(string fileName)` → `Id` (Guid), `FileName`, `CreatedAt` (DateTime.UtcNow), `Status` = `Processing`, `Requirements`, `Error` (string?); métodos `AddRequirement(Requirement)`, `Complete()` → `Completed`, `Fail(string error)` → `Failed` + `Error`.
  - `Analysis.Rehydrate(Guid id, string fileName, DateTime createdAt, AnalysisStatus status, string? error, IReadOnlyList<Requirement> requirements)` — reconstrucción desde persistencia sin validar invariantes de creación.
  - `enum AnalysisStatus { Processing, Completed, Failed }`.

- [ ] **Step 1: Test que falla**

`tests/RequirementsCopilot.Tests/Domain/EvaluationTests.cs`:

```csharp
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Domain;

public class EvaluationTests
{
    private static CriterionScore Score(int value) => CriterionScore.Create("Claridad", value, "obs");

    [Fact]
    public void Create_PromedioSobreUmbral_Pasa()
    {
        var evaluation = Evaluation.Create(new[] { Score(4), Score(4), Score(3) }, threshold: 3.5);
        Assert.Equal(3.67, Math.Round(evaluation.Average, 2));
        Assert.True(evaluation.Passed);
    }

    [Fact]
    public void Create_PromedioBajoUmbral_NoPasa()
    {
        var evaluation = Evaluation.Create(new[] { Score(3), Score(3) }, threshold: 3.5);
        Assert.False(evaluation.Passed);
    }

    [Fact]
    public void Create_SinScores_Lanza()
        => Assert.Throws<ArgumentException>(() => Evaluation.Create(Array.Empty<CriterionScore>(), 3.5));

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void CriterionScore_FueraDeRango_Lanza(int score)
        => Assert.Throws<ArgumentOutOfRangeException>(() => CriterionScore.Create("Claridad", score, "obs"));
}
```

`tests/RequirementsCopilot.Tests/Domain/AnalysisTests.cs`:

```csharp
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Domain;

public class AnalysisTests
{
    [Fact]
    public void Create_IniciaEnProcessing()
    {
        var analysis = Analysis.Create("spec.pdf");
        Assert.Equal(AnalysisStatus.Processing, analysis.Status);
        Assert.NotEqual(Guid.Empty, analysis.Id);
        Assert.Empty(analysis.Requirements);
    }

    [Fact]
    public void Fail_GuardaErrorYEstado()
    {
        var analysis = Analysis.Create("spec.pdf");
        analysis.Fail("LLM no disponible");
        Assert.Equal(AnalysisStatus.Failed, analysis.Status);
        Assert.Equal("LLM no disponible", analysis.Error);
    }

    [Fact]
    public void FlujoCompleto_RequerimientoConHistoriaYCaso()
    {
        var analysis = Analysis.Create("spec.txt");
        var requirement = Requirement.Create("REQ-001", "El sistema debe X", "Pagos");
        requirement.Evaluate(Evaluation.Create(new[] { CriterionScore.Create("Claridad", 5, "ok") }, 3.5));
        var story = UserStory.Create("cajero", "registrar pago", "cerrar la venta", new[] { "dado A entonces B" });
        story.AttachTestCase(TestCase.Create("Pago exitoso", new[] { "sesión activa" }, new[] { "abrir caja" }, "pago registrado"));
        requirement.AddStory(story);
        analysis.AddRequirement(requirement);
        analysis.Complete();

        Assert.Equal(AnalysisStatus.Completed, analysis.Status);
        Assert.Single(analysis.Requirements);
        Assert.True(analysis.Requirements[0].Evaluation!.Passed);
        Assert.NotNull(analysis.Requirements[0].Stories[0].TestCase);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~RequirementsCopilot.Tests.Domain"`
Expected: FAIL (no compila: tipos no existen).

- [ ] **Step 3: Implementar dominio**

`src/RequirementsCopilot.Domain/Analyses/AnalysisStatus.cs`:

```csharp
namespace RequirementsCopilot.Domain.Analyses;

public enum AnalysisStatus { Processing, Completed, Failed }
```

`src/RequirementsCopilot.Domain/Analyses/CriterionScore.cs`:

```csharp
namespace RequirementsCopilot.Domain.Analyses;

public sealed record CriterionScore
{
    public string Criterion { get; }
    public int Score { get; }
    public string Observation { get; }

    private CriterionScore(string criterion, int score, string observation)
        => (Criterion, Score, Observation) = (criterion, score, observation);

    public static CriterionScore Create(string criterion, int score, string observation)
    {
        if (string.IsNullOrWhiteSpace(criterion))
        {
            throw new ArgumentException("El criterio no puede estar vacío.", nameof(criterion));
        }
        if (score is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "El score debe estar entre 1 y 5.");
        }
        return new CriterionScore(criterion.Trim(), score, observation?.Trim() ?? string.Empty);
    }
}
```

`src/RequirementsCopilot.Domain/Analyses/Evaluation.cs`:

```csharp
namespace RequirementsCopilot.Domain.Analyses;

public sealed record Evaluation
{
    public IReadOnlyList<CriterionScore> Scores { get; }
    public double Threshold { get; }
    public double Average { get; }
    public bool Passed { get; }

    private Evaluation(IReadOnlyList<CriterionScore> scores, double threshold)
    {
        Scores = scores;
        Threshold = threshold;
        Average = scores.Average(s => s.Score);
        Passed = Average >= threshold;
    }

    public static Evaluation Create(IReadOnlyList<CriterionScore> scores, double threshold)
    {
        if (scores is null || scores.Count == 0)
        {
            throw new ArgumentException("La evaluación requiere al menos un criterio.", nameof(scores));
        }
        return new Evaluation(scores.ToArray(), threshold);
    }
}
```

`src/RequirementsCopilot.Domain/Analyses/TestCase.cs`:

```csharp
namespace RequirementsCopilot.Domain.Analyses;

public sealed record TestCase
{
    public string Title { get; }
    public IReadOnlyList<string> Preconditions { get; }
    public IReadOnlyList<string> Steps { get; }
    public string ExpectedResult { get; }

    private TestCase(string title, IReadOnlyList<string> preconditions, IReadOnlyList<string> steps, string expectedResult)
        => (Title, Preconditions, Steps, ExpectedResult) = (title, preconditions, steps, expectedResult);

    public static TestCase Create(string title, IReadOnlyList<string> preconditions, IReadOnlyList<string> steps, string expectedResult)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("El caso de prueba requiere título.", nameof(title));
        }
        return new TestCase(title.Trim(), preconditions?.ToArray() ?? Array.Empty<string>(),
            steps?.ToArray() ?? Array.Empty<string>(), expectedResult?.Trim() ?? string.Empty);
    }
}
```

`src/RequirementsCopilot.Domain/Analyses/UserStory.cs`:

```csharp
namespace RequirementsCopilot.Domain.Analyses;

public sealed class UserStory
{
    public string Role { get; }
    public string Goal { get; }
    public string Benefit { get; }
    public IReadOnlyList<string> AcceptanceCriteria { get; }
    public TestCase? TestCase { get; private set; }

    private UserStory(string role, string goal, string benefit, IReadOnlyList<string> acceptanceCriteria)
        => (Role, Goal, Benefit, AcceptanceCriteria) = (role, goal, benefit, acceptanceCriteria);

    public static UserStory Create(string role, string goal, string benefit, IReadOnlyList<string> acceptanceCriteria)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            throw new ArgumentException("La historia requiere el objetivo (quiero).", nameof(goal));
        }
        return new UserStory(role?.Trim() ?? string.Empty, goal.Trim(), benefit?.Trim() ?? string.Empty,
            acceptanceCriteria?.ToArray() ?? Array.Empty<string>());
    }

    public void AttachTestCase(TestCase testCase) => TestCase = testCase;
}
```

`src/RequirementsCopilot.Domain/Analyses/Requirement.cs`:

```csharp
namespace RequirementsCopilot.Domain.Analyses;

public sealed class Requirement
{
    private readonly List<UserStory> _stories = new();

    public string Code { get; }
    public string Text { get; }
    public string Area { get; }
    public Evaluation? Evaluation { get; private set; }
    public IReadOnlyList<UserStory> Stories => _stories;

    private Requirement(string code, string text, string area) => (Code, Text, Area) = (code, text, area);

    public static Requirement Create(string code, string text, string area)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("El requerimiento requiere código.", nameof(code));
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("El requerimiento requiere texto.", nameof(text));
        }
        return new Requirement(code.Trim(), text.Trim(), string.IsNullOrWhiteSpace(area) ? "General" : area.Trim());
    }

    public void Evaluate(Evaluation evaluation) => Evaluation = evaluation;

    public void AddStory(UserStory story) => _stories.Add(story);
}
```

`src/RequirementsCopilot.Domain/Analyses/Analysis.cs`:

```csharp
namespace RequirementsCopilot.Domain.Analyses;

public sealed class Analysis
{
    private readonly List<Requirement> _requirements = new();

    public Guid Id { get; }
    public string FileName { get; }
    public DateTime CreatedAt { get; }
    public AnalysisStatus Status { get; private set; }
    public string? Error { get; private set; }
    public IReadOnlyList<Requirement> Requirements => _requirements;

    private Analysis(Guid id, string fileName, DateTime createdAt, AnalysisStatus status, string? error)
        => (Id, FileName, CreatedAt, Status, Error) = (id, fileName, createdAt, status, error);

    public static Analysis Create(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("El análisis requiere nombre de archivo.", nameof(fileName));
        }
        return new Analysis(Guid.NewGuid(), fileName.Trim(), DateTime.UtcNow, AnalysisStatus.Processing, null);
    }

    public static Analysis Rehydrate(Guid id, string fileName, DateTime createdAt, AnalysisStatus status,
        string? error, IReadOnlyList<Requirement> requirements)
    {
        var analysis = new Analysis(id, fileName, createdAt, status, error);
        analysis._requirements.AddRange(requirements);
        return analysis;
    }

    public void AddRequirement(Requirement requirement) => _requirements.Add(requirement);

    public void Complete() => Status = AnalysisStatus.Completed;

    public void Fail(string error)
    {
        Status = AnalysisStatus.Failed;
        Error = error;
    }
}
```

- [ ] **Step 4: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~RequirementsCopilot.Tests.Domain"`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: dominio Analysis con rúbrica, historias y casos de prueba"
```

---

### Task 3: Puertos de Application + JsonText

**Files:**
- Create: `src/RequirementsCopilot.Application/Analyses/IChatCompletion.cs`, `ChatPrompt.cs`, `ChatResult.cs`, `IAnalysisRepository.cs`, `IDocumentTextExtractor.cs`, `JsonText.cs`, `AnalysisOptions.cs`
- Test: `tests/RequirementsCopilot.Tests/Application/JsonTextTests.cs`

**Interfaces:**
- Produces (namespace `RequirementsCopilot.Application.Analyses`):
  - `record ChatPrompt(string Agent, string Instructions, string Input)` — a diferencia de JYDE, las instrucciones viajan en el prompt (viven en el código, no en agentes publicados en Foundry).
  - `record ChatResult(string Text)`
  - `interface IChatCompletion { Task<ChatResult> CompleteAsync(ChatPrompt prompt, CancellationToken cancellationToken = default); }`
  - `interface IAnalysisRepository { Task SaveAsync(Analysis analysis, CancellationToken ct = default); Task<Analysis?> GetByIdAsync(Guid id, CancellationToken ct = default); Task<IReadOnlyList<Analysis>> GetAllAsync(CancellationToken ct = default); }`
  - `interface IDocumentTextExtractor { Task<string> ExtractAsync(Stream content, string fileName, CancellationToken ct = default); }`
  - `static class JsonText { public static string? FirstJsonObject(string? text); }` — **público** (lo usan los 4 agentes y tests).
  - `record AnalysisOptions { double PassThreshold = 3.5 }`

- [ ] **Step 1: Test que falla** — `tests/RequirementsCopilot.Tests/Application/JsonTextTests.cs`:

```csharp
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class JsonTextTests
{
    [Fact]
    public void FirstJsonObject_ConProsaYVallas_ExtraeObjeto()
    {
        const string text = "Claro, aquí está:\n```json\n{\"a\":1}\n```\ngracias";
        Assert.Equal("{\"a\":1}", JsonText.FirstJsonObject(text));
    }

    [Fact]
    public void FirstJsonObject_ObjetoDuplicado_TomaElPrimero()
        => Assert.Equal("{\"a\":1}", JsonText.FirstJsonObject("{\"a\":1}{\"a\":2}"));

    [Fact]
    public void FirstJsonObject_LlavesDentroDeCadenas_NoRompeBalance()
        => Assert.Equal("{\"a\":\"x}y\"}", JsonText.FirstJsonObject("{\"a\":\"x}y\"}"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin json")]
    [InlineData("{\"abierto\":1")]
    public void FirstJsonObject_SinObjetoCerrado_DevuelveNull(string? text)
        => Assert.Null(JsonText.FirstJsonObject(text));
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~JsonTextTests"`
Expected: FAIL (no compila).

- [ ] **Step 3: Implementar**

`src/RequirementsCopilot.Application/Analyses/ChatPrompt.cs`:

```csharp
namespace RequirementsCopilot.Application.Analyses;

public sealed record ChatPrompt(string Agent, string Instructions, string Input);
```

`src/RequirementsCopilot.Application/Analyses/ChatResult.cs`:

```csharp
namespace RequirementsCopilot.Application.Analyses;

public sealed record ChatResult(string Text);
```

`src/RequirementsCopilot.Application/Analyses/IChatCompletion.cs`:

```csharp
namespace RequirementsCopilot.Application.Analyses;

public interface IChatCompletion
{
    Task<ChatResult> CompleteAsync(ChatPrompt prompt, CancellationToken cancellationToken = default);
}
```

`src/RequirementsCopilot.Application/Analyses/IAnalysisRepository.cs`:

```csharp
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public interface IAnalysisRepository
{
    Task SaveAsync(Analysis analysis, CancellationToken cancellationToken = default);
    Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Analysis>> GetAllAsync(CancellationToken cancellationToken = default);
}
```

`src/RequirementsCopilot.Application/Analyses/IDocumentTextExtractor.cs`:

```csharp
namespace RequirementsCopilot.Application.Analyses;

public interface IDocumentTextExtractor
{
    Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}
```

`src/RequirementsCopilot.Application/Analyses/AnalysisOptions.cs`:

```csharp
namespace RequirementsCopilot.Application.Analyses;

public sealed record AnalysisOptions
{
    public double PassThreshold { get; init; } = 3.5;
}
```

`src/RequirementsCopilot.Application/Analyses/JsonText.cs`: copiar tal cual `JYDE.OpenDataCopilot.Application/Conversation/JsonText.cs` (algoritmo de primer objeto balanceado ignorando llaves dentro de cadenas, con `AdvanceString`), cambiando namespace a `RequirementsCopilot.Application.Analyses` y visibilidad a `public static class JsonText` / `public static string? FirstJsonObject`.

- [ ] **Step 4: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~JsonTextTests"`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: puertos de Application y utilidad JsonText"
```

---

### Task 4: RequirementExtractorAgent

**Files:**
- Create: `src/RequirementsCopilot.Application/Analyses/Agents/RequirementExtractorAgent.cs`
- Test: `tests/RequirementsCopilot.Tests/Application/RequirementExtractorAgentTests.cs`, `tests/RequirementsCopilot.Tests/Application/StubChatCompletion.cs`

**Interfaces:**
- Consumes: `IChatCompletion`, `JsonText`, `Requirement.Create(code, text, area)`.
- Produces (namespace `RequirementsCopilot.Application.Analyses.Agents`):
  - `class RequirementExtractorAgent { ctor(IChatCompletion chat); Task<IReadOnlyList<Requirement>> ExtractAsync(string documentText, CancellationToken ct = default); }`
  - Lanza `InvalidOperationException` con mensaje en español si la respuesta no trae JSON parseable.
- Test double compartido: `class StubChatCompletion : IChatCompletion { public Func<ChatPrompt, string> Reply; public List<ChatPrompt> Prompts; }` — lo reutilizan Tasks 5–8.

- [ ] **Step 1: Test que falla**

`tests/RequirementsCopilot.Tests/Application/StubChatCompletion.cs`:

```csharp
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Tests.Application;

public sealed class StubChatCompletion : IChatCompletion
{
    public Func<ChatPrompt, string> Reply { get; set; } = _ => "{}";
    public List<ChatPrompt> Prompts { get; } = new();

    public Task<ChatResult> CompleteAsync(ChatPrompt prompt, CancellationToken cancellationToken = default)
    {
        Prompts.Add(prompt);
        return Task.FromResult(new ChatResult(Reply(prompt)));
    }
}
```

`tests/RequirementsCopilot.Tests/Application/RequirementExtractorAgentTests.cs`:

```csharp
using RequirementsCopilot.Application.Analyses.Agents;

namespace RequirementsCopilot.Tests.Application;

public class RequirementExtractorAgentTests
{
    [Fact]
    public async Task ExtractAsync_JsonConProsa_DevuelveRequerimientos()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "Listo:\n{\"requerimientos\":[{\"codigo\":\"REQ-001\",\"texto\":\"El sistema debe registrar pagos\",\"area\":\"Pagos\"}]}",
        };
        var agent = new RequirementExtractorAgent(chat);

        var requirements = await agent.ExtractAsync("documento de prueba");

        var requirement = Assert.Single(requirements);
        Assert.Equal("REQ-001", requirement.Code);
        Assert.Equal("Pagos", requirement.Area);
        Assert.Equal("requirement-extractor-agent", chat.Prompts[0].Agent);
        Assert.Contains("documento de prueba", chat.Prompts[0].Input);
    }

    [Fact]
    public async Task ExtractAsync_SinJson_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "no puedo" };
        var agent = new RequirementExtractorAgent(chat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ExtractAsync("doc"));
    }

    [Fact]
    public async Task ExtractAsync_ListaVacia_DevuelveVacio()
    {
        var chat = new StubChatCompletion { Reply = _ => "{\"requerimientos\":[]}" };
        var agent = new RequirementExtractorAgent(chat);
        Assert.Empty(await agent.ExtractAsync("doc"));
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~RequirementExtractorAgentTests"`
Expected: FAIL (no compila).

- [ ] **Step 3: Implementar** — `src/RequirementsCopilot.Application/Analyses/Agents/RequirementExtractorAgent.cs`:

```csharp
using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class RequirementExtractorAgent
{
    public const string AgentName = "requirement-extractor-agent";

    private const string Instructions =
        "Eres un analista de requerimientos. Extrae del documento TODOS los requerimientos de software. " +
        "Asigna a cada uno un código secuencial (REQ-001, REQ-002…) y un área funcional corta (ej. Pagos, Seguridad, Reportes; usa \"General\" si no es claro). " +
        "Responde ÚNICAMENTE este JSON: {\"requerimientos\":[{\"codigo\":\"REQ-001\",\"texto\":\"...\",\"area\":\"...\"}]} " +
        "Si el documento no contiene requerimientos, responde {\"requerimientos\":[]}. No inventes requerimientos.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public RequirementExtractorAgent(IChatCompletion chat) => _chat = chat;

    public async Task<IReadOnlyList<Requirement>> ExtractAsync(string documentText, CancellationToken cancellationToken = default)
    {
        ChatResult result = await _chat.CompleteAsync(
            new ChatPrompt(AgentName, Instructions, $"Documento:\n{documentText}"), cancellationToken);

        string json = JsonText.FirstJsonObject(result.Text)
            ?? throw new InvalidOperationException("El agente extractor no devolvió JSON válido.");
        ExtractorReply reply = JsonSerializer.Deserialize<ExtractorReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente extractor devolvió una respuesta vacía.");

        return (reply.Requerimientos ?? new List<ExtractedRequirement>())
            .Where(r => !string.IsNullOrWhiteSpace(r.Codigo) && !string.IsNullOrWhiteSpace(r.Texto))
            .Select(r => Requirement.Create(r.Codigo!, r.Texto!, r.Area ?? "General"))
            .ToArray();
    }

    private sealed record ExtractorReply(List<ExtractedRequirement>? Requerimientos);

    private sealed record ExtractedRequirement(string? Codigo, string? Texto, string? Area);
}
```

- [ ] **Step 4: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~RequirementExtractorAgentTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: agente extractor de requerimientos"
```

---

### Task 5: RequirementEvaluatorAgent

**Files:**
- Create: `src/RequirementsCopilot.Application/Analyses/Agents/RequirementEvaluatorAgent.cs`
- Test: `tests/RequirementsCopilot.Tests/Application/RequirementEvaluatorAgentTests.cs`

**Interfaces:**
- Consumes: `IChatCompletion`, `JsonText`, `Evaluation.Create`, `CriterionScore.Create`, `StubChatCompletion` (Task 4).
- Produces: `class RequirementEvaluatorAgent { ctor(IChatCompletion chat); Task<Evaluation> EvaluateAsync(Requirement requirement, double threshold, CancellationToken ct = default); }`

- [ ] **Step 1: Test que falla** — `tests/RequirementsCopilot.Tests/Application/RequirementEvaluatorAgentTests.cs`:

```csharp
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class RequirementEvaluatorAgentTests
{
    private static Requirement Req() => Requirement.Create("REQ-001", "El sistema debe registrar pagos", "Pagos");

    [Fact]
    public async Task EvaluateAsync_RubricaCompleta_CalculaVeredicto()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"criterios\":[" +
                "{\"nombre\":\"Claridad\",\"score\":4,\"observacion\":\"clara\"}," +
                "{\"nombre\":\"Completitud\",\"score\":4,\"observacion\":\"completa\"}," +
                "{\"nombre\":\"Verificabilidad\",\"score\":4,\"observacion\":\"medible\"}," +
                "{\"nombre\":\"Consistencia\",\"score\":4,\"observacion\":\"consistente\"}," +
                "{\"nombre\":\"Factibilidad\",\"score\":4,\"observacion\":\"viable\"}]}",
        };
        var agent = new RequirementEvaluatorAgent(chat);

        var evaluation = await agent.EvaluateAsync(Req(), threshold: 3.5);

        Assert.Equal(5, evaluation.Scores.Count);
        Assert.Equal(4.0, evaluation.Average);
        Assert.True(evaluation.Passed);
        Assert.Equal("requirement-evaluator-agent", chat.Prompts[0].Agent);
        Assert.Contains("El sistema debe registrar pagos", chat.Prompts[0].Input);
    }

    [Fact]
    public async Task EvaluateAsync_ScoresBajos_NoPasa()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"criterios\":[{\"nombre\":\"Claridad\",\"score\":2,\"observacion\":\"ambigua\"}]}",
        };
        var agent = new RequirementEvaluatorAgent(chat);
        var evaluation = await agent.EvaluateAsync(Req(), threshold: 3.5);
        Assert.False(evaluation.Passed);
    }

    [Fact]
    public async Task EvaluateAsync_SinJson_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "sin datos" };
        var agent = new RequirementEvaluatorAgent(chat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.EvaluateAsync(Req(), 3.5));
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~RequirementEvaluatorAgentTests"`
Expected: FAIL (no compila).

- [ ] **Step 3: Implementar** — `src/RequirementsCopilot.Application/Analyses/Agents/RequirementEvaluatorAgent.cs`:

```csharp
using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class RequirementEvaluatorAgent
{
    public const string AgentName = "requirement-evaluator-agent";

    private const string Instructions =
        "Eres un evaluador de calidad de requerimientos de software. Evalúa el requerimiento dado contra CADA uno de estos " +
        "criterios: Claridad, Completitud, Verificabilidad, Consistencia, Factibilidad. Asigna score entero de 1 (muy deficiente) " +
        "a 5 (excelente) y una observación breve que justifique el score. " +
        "Responde ÚNICAMENTE este JSON: {\"criterios\":[{\"nombre\":\"Claridad\",\"score\":4,\"observacion\":\"...\"}]} " +
        "con exactamente los 5 criterios. Sé estricto: un requerimiento ambiguo o no medible no merece más de 2 en ese criterio.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public RequirementEvaluatorAgent(IChatCompletion chat) => _chat = chat;

    public async Task<Evaluation> EvaluateAsync(Requirement requirement, double threshold, CancellationToken cancellationToken = default)
    {
        ChatResult result = await _chat.CompleteAsync(
            new ChatPrompt(AgentName, Instructions, $"Requerimiento {requirement.Code} (área {requirement.Area}):\n{requirement.Text}"),
            cancellationToken);

        string json = JsonText.FirstJsonObject(result.Text)
            ?? throw new InvalidOperationException("El agente evaluador no devolvió JSON válido.");
        EvaluatorReply reply = JsonSerializer.Deserialize<EvaluatorReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente evaluador devolvió una respuesta vacía.");

        CriterionScore[] scores = (reply.Criterios ?? new List<EvaluatedCriterion>())
            .Where(c => !string.IsNullOrWhiteSpace(c.Nombre) && c.Score is >= 1 and <= 5)
            .Select(c => CriterionScore.Create(c.Nombre!, c.Score, c.Observacion ?? string.Empty))
            .ToArray();
        if (scores.Length == 0)
        {
            throw new InvalidOperationException("El agente evaluador no devolvió criterios válidos.");
        }

        return Evaluation.Create(scores, threshold);
    }

    private sealed record EvaluatorReply(List<EvaluatedCriterion>? Criterios);

    private sealed record EvaluatedCriterion(string? Nombre, int Score, string? Observacion);
}
```

- [ ] **Step 4: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~RequirementEvaluatorAgentTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: agente evaluador con rúbrica de 5 criterios"
```

---

### Task 6: StoryWriterAgent

**Files:**
- Create: `src/RequirementsCopilot.Application/Analyses/Agents/StoryWriterAgent.cs`
- Test: `tests/RequirementsCopilot.Tests/Application/StoryWriterAgentTests.cs`

**Interfaces:**
- Consumes: `IChatCompletion`, `JsonText`, `UserStory.Create`, `StubChatCompletion`.
- Produces: `class StoryWriterAgent { ctor(IChatCompletion chat); Task<IReadOnlyList<UserStory>> WriteAsync(Requirement requirement, CancellationToken ct = default); }`

- [ ] **Step 1: Test que falla** — `tests/RequirementsCopilot.Tests/Application/StoryWriterAgentTests.cs`:

```csharp
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class StoryWriterAgentTests
{
    [Fact]
    public async Task WriteAsync_DevuelveHistoriasConCriterios()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"historias\":[{\"rol\":\"cajero\",\"quiero\":\"registrar un pago\",\"para\":\"cerrar la venta\"," +
                "\"criteriosAceptacion\":[\"dado un monto válido, el pago queda registrado\"]}]}",
        };
        var agent = new StoryWriterAgent(chat);
        var requirement = Requirement.Create("REQ-001", "El sistema debe registrar pagos", "Pagos");

        var stories = await agent.WriteAsync(requirement);

        var story = Assert.Single(stories);
        Assert.Equal("cajero", story.Role);
        Assert.Equal("registrar un pago", story.Goal);
        Assert.Single(story.AcceptanceCriteria);
        Assert.Equal("story-writer-agent", chat.Prompts[0].Agent);
    }

    [Fact]
    public async Task WriteAsync_SinJson_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "..." };
        var agent = new StoryWriterAgent(chat);
        var requirement = Requirement.Create("REQ-001", "texto", "General");
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.WriteAsync(requirement));
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~StoryWriterAgentTests"`
Expected: FAIL (no compila).

- [ ] **Step 3: Implementar** — `src/RequirementsCopilot.Application/Analyses/Agents/StoryWriterAgent.cs`:

```csharp
using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class StoryWriterAgent
{
    public const string AgentName = "story-writer-agent";

    private const string Instructions =
        "Eres un product owner. A partir del requerimiento aprobado, escribe las historias de usuario necesarias " +
        "(mínimo 1, máximo 4), cada una con rol, objetivo (quiero), beneficio (para) y de 1 a 4 criterios de aceptación " +
        "verificables en formato dado/cuando/entonces. " +
        "Responde ÚNICAMENTE este JSON: {\"historias\":[{\"rol\":\"...\",\"quiero\":\"...\",\"para\":\"...\",\"criteriosAceptacion\":[\"...\"]}]} " +
        "No inventes funcionalidad que el requerimiento no mencione.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public StoryWriterAgent(IChatCompletion chat) => _chat = chat;

    public async Task<IReadOnlyList<UserStory>> WriteAsync(Requirement requirement, CancellationToken cancellationToken = default)
    {
        ChatResult result = await _chat.CompleteAsync(
            new ChatPrompt(AgentName, Instructions, $"Requerimiento {requirement.Code} (área {requirement.Area}):\n{requirement.Text}"),
            cancellationToken);

        string json = JsonText.FirstJsonObject(result.Text)
            ?? throw new InvalidOperationException("El agente de historias no devolvió JSON válido.");
        StoriesReply reply = JsonSerializer.Deserialize<StoriesReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente de historias devolvió una respuesta vacía.");

        return (reply.Historias ?? new List<StoryItem>())
            .Where(h => !string.IsNullOrWhiteSpace(h.Quiero))
            .Select(h => UserStory.Create(h.Rol ?? string.Empty, h.Quiero!, h.Para ?? string.Empty,
                h.CriteriosAceptacion ?? new List<string>()))
            .ToArray();
    }

    private sealed record StoriesReply(List<StoryItem>? Historias);

    private sealed record StoryItem(string? Rol, string? Quiero, string? Para, List<string>? CriteriosAceptacion);
}
```

- [ ] **Step 4: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~StoryWriterAgentTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: agente escritor de historias de usuario"
```

---

### Task 7: TestCaseWriterAgent

**Files:**
- Create: `src/RequirementsCopilot.Application/Analyses/Agents/TestCaseWriterAgent.cs`
- Test: `tests/RequirementsCopilot.Tests/Application/TestCaseWriterAgentTests.cs`

**Interfaces:**
- Consumes: `IChatCompletion`, `JsonText`, `TestCase.Create`, `StubChatCompletion`.
- Produces: `class TestCaseWriterAgent { ctor(IChatCompletion chat); Task<TestCase> WriteAsync(UserStory story, CancellationToken ct = default); }`

- [ ] **Step 1: Test que falla** — `tests/RequirementsCopilot.Tests/Application/TestCaseWriterAgentTests.cs`:

```csharp
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class TestCaseWriterAgentTests
{
    private static UserStory Story()
        => UserStory.Create("cajero", "registrar un pago", "cerrar la venta", new[] { "dado un monto válido, queda registrado" });

    [Fact]
    public async Task WriteAsync_DevuelveCasoDePrueba()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"titulo\":\"Pago exitoso\",\"precondiciones\":[\"sesión de caja activa\"]," +
                "\"pasos\":[\"abrir caja\",\"registrar monto\"],\"resultadoEsperado\":\"pago registrado con consecutivo\"}",
        };
        var agent = new TestCaseWriterAgent(chat);

        var testCase = await agent.WriteAsync(Story());

        Assert.Equal("Pago exitoso", testCase.Title);
        Assert.Equal(2, testCase.Steps.Count);
        Assert.Equal("test-case-writer-agent", chat.Prompts[0].Agent);
        Assert.Contains("registrar un pago", chat.Prompts[0].Input);
    }

    [Fact]
    public async Task WriteAsync_SinJson_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "nada" };
        var agent = new TestCaseWriterAgent(chat);
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.WriteAsync(Story()));
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~TestCaseWriterAgentTests"`
Expected: FAIL (no compila).

- [ ] **Step 3: Implementar** — `src/RequirementsCopilot.Application/Analyses/Agents/TestCaseWriterAgent.cs`:

```csharp
using System.Text.Json;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

public sealed class TestCaseWriterAgent
{
    public const string AgentName = "test-case-writer-agent";

    private const string Instructions =
        "Eres un ingeniero de QA. A partir de la historia de usuario dada, escribe UN caso de prueba funcional que valide " +
        "sus criterios de aceptación: título corto, precondiciones, pasos numerables concretos y resultado esperado verificable. " +
        "Responde ÚNICAMENTE este JSON: {\"titulo\":\"...\",\"precondiciones\":[\"...\"],\"pasos\":[\"...\"],\"resultadoEsperado\":\"...\"}";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatCompletion _chat;

    public TestCaseWriterAgent(IChatCompletion chat) => _chat = chat;

    public async Task<TestCase> WriteAsync(UserStory story, CancellationToken cancellationToken = default)
    {
        string input = $"Historia: como {story.Role}, quiero {story.Goal}, para {story.Benefit}.\n" +
            $"Criterios de aceptación:\n- {string.Join("\n- ", story.AcceptanceCriteria)}";
        ChatResult result = await _chat.CompleteAsync(new ChatPrompt(AgentName, Instructions, input), cancellationToken);

        string json = JsonText.FirstJsonObject(result.Text)
            ?? throw new InvalidOperationException("El agente de casos de prueba no devolvió JSON válido.");
        TestCaseReply reply = JsonSerializer.Deserialize<TestCaseReply>(json, JsonOptions)
            ?? throw new InvalidOperationException("El agente de casos de prueba devolvió una respuesta vacía.");
        if (string.IsNullOrWhiteSpace(reply.Titulo))
        {
            throw new InvalidOperationException("El agente de casos de prueba no devolvió título.");
        }

        return TestCase.Create(reply.Titulo!, reply.Precondiciones ?? new List<string>(),
            reply.Pasos ?? new List<string>(), reply.ResultadoEsperado ?? string.Empty);
    }

    private sealed record TestCaseReply(string? Titulo, List<string>? Precondiciones, List<string>? Pasos, string? ResultadoEsperado);
}
```

- [ ] **Step 4: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~TestCaseWriterAgentTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: agente escritor de casos de prueba"
```

---

### Task 8: AnalysisEvent + AnalysisOrchestrator

**Files:**
- Create: `src/RequirementsCopilot.Application/Analyses/AnalysisEvent.cs`, `src/RequirementsCopilot.Application/Analyses/AnalysisOrchestrator.cs`
- Test: `tests/RequirementsCopilot.Tests/Application/AnalysisOrchestratorTests.cs`

**Interfaces:**
- Consumes: los 4 agentes (Tasks 4–7), `IDocumentTextExtractor`, `IAnalysisRepository`, `AnalysisOptions`, dominio.
- Produces (namespace `RequirementsCopilot.Application.Analyses`):
  - `enum AnalysisEventKind { Status, Requirement, Evaluation, Story, TestCase, Done, Error }`
  - DTOs: `CriterionDto(string Nombre, int Score, string Observacion)`, `RequirementDto(string Codigo, string Texto, string Area)`, `EvaluationDto(string RequirementCode, IReadOnlyList<CriterionDto> Criterios, double Promedio, double Umbral, bool Pasa)`, `StoryDto(string RequirementCode, int StoryIndex, string Rol, string Quiero, string Para, IReadOnlyList<string> CriteriosAceptacion)`, `TestCaseDto(string RequirementCode, int StoryIndex, string Titulo, IReadOnlyList<string> Precondiciones, IReadOnlyList<string> Pasos, string ResultadoEsperado)` — la Api solo ve estos DTOs, nunca el dominio.
  - `record AnalysisEvent` con `Kind` y payloads opcionales + factories estáticas: `Status(string)`, `FromRequirement(Requirement)`, `FromEvaluation(Requirement)`, `FromStory(string code, int index, UserStory)`, `FromTestCase(string code, int index, TestCase)`, `Done(Guid)`, `Error(string)`.
  - `class AnalysisOrchestrator { ctor(IDocumentTextExtractor, RequirementExtractorAgent, RequirementEvaluatorAgent, StoryWriterAgent, TestCaseWriterAgent, IAnalysisRepository, AnalysisOptions); IAsyncEnumerable<AnalysisEvent> AnalyzeAsync(Stream content, string fileName, CancellationToken ct = default); }`

- [ ] **Step 1: Test que falla** — `tests/RequirementsCopilot.Tests/Application/AnalysisOrchestratorTests.cs`:

```csharp
using System.Text;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class AnalysisOrchestratorTests
{
    private sealed class StubRepository : IAnalysisRepository
    {
        public Analysis? Saved { get; private set; }
        public Task SaveAsync(Analysis analysis, CancellationToken ct = default) { Saved = analysis; return Task.CompletedTask; }
        public Task<Analysis?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Analysis?>(null);
        public Task<IReadOnlyList<Analysis>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Analysis>>(Array.Empty<Analysis>());
    }

    private sealed class StubExtractor : IDocumentTextExtractor
    {
        public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
            => Task.FromResult("texto del documento");
    }

    private static string HighRubric() =>
        "{\"criterios\":[{\"nombre\":\"Claridad\",\"score\":5,\"observacion\":\"ok\"},{\"nombre\":\"Completitud\",\"score\":5,\"observacion\":\"ok\"}," +
        "{\"nombre\":\"Verificabilidad\",\"score\":5,\"observacion\":\"ok\"},{\"nombre\":\"Consistencia\",\"score\":5,\"observacion\":\"ok\"}," +
        "{\"nombre\":\"Factibilidad\",\"score\":5,\"observacion\":\"ok\"}]}";

    private static string LowRubric() =>
        "{\"criterios\":[{\"nombre\":\"Claridad\",\"score\":1,\"observacion\":\"ambiguo\"},{\"nombre\":\"Completitud\",\"score\":2,\"observacion\":\"incompleto\"}," +
        "{\"nombre\":\"Verificabilidad\",\"score\":1,\"observacion\":\"no medible\"},{\"nombre\":\"Consistencia\",\"score\":2,\"observacion\":\"ok\"}," +
        "{\"nombre\":\"Factibilidad\",\"score\":2,\"observacion\":\"dudosa\"}]}";

    private static StubChatCompletion PipelineChat() => new()
    {
        Reply = prompt => prompt.Agent switch
        {
            RequirementExtractorAgent.AgentName =>
                "{\"requerimientos\":[{\"codigo\":\"REQ-001\",\"texto\":\"El sistema debe registrar pagos\",\"area\":\"Pagos\"}," +
                "{\"codigo\":\"REQ-002\",\"texto\":\"El sistema debe ser rápido\",\"area\":\"General\"}]}",
            RequirementEvaluatorAgent.AgentName => prompt.Input.Contains("REQ-001") ? HighRubric() : LowRubric(),
            StoryWriterAgent.AgentName =>
                "{\"historias\":[{\"rol\":\"cajero\",\"quiero\":\"registrar un pago\",\"para\":\"cerrar la venta\",\"criteriosAceptacion\":[\"dado A entonces B\"]}]}",
            TestCaseWriterAgent.AgentName =>
                "{\"titulo\":\"Pago exitoso\",\"precondiciones\":[\"caja abierta\"],\"pasos\":[\"registrar\"],\"resultadoEsperado\":\"registrado\"}",
            _ => "{}",
        },
    };

    private static AnalysisOrchestrator Orchestrator(StubChatCompletion chat, StubRepository repository) => new(
        new StubExtractor(),
        new RequirementExtractorAgent(chat),
        new RequirementEvaluatorAgent(chat),
        new StoryWriterAgent(chat),
        new TestCaseWriterAgent(chat),
        repository,
        new AnalysisOptions());

    private static async Task<List<AnalysisEvent>> Collect(AnalysisOrchestrator orchestrator)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("doc"));
        var events = new List<AnalysisEvent>();
        await foreach (var analysisEvent in orchestrator.AnalyzeAsync(stream, "spec.txt"))
        {
            events.Add(analysisEvent);
        }
        return events;
    }

    [Fact]
    public async Task AnalyzeAsync_PipelineCompleto_EmiteEventosEnOrdenYPersiste()
    {
        var repository = new StubRepository();
        var events = await Collect(Orchestrator(PipelineChat(), repository));

        Assert.Equal(
            new[]
            {
                AnalysisEventKind.Status, AnalysisEventKind.Requirement, AnalysisEventKind.Evaluation,
                AnalysisEventKind.Story, AnalysisEventKind.TestCase, AnalysisEventKind.Requirement,
                AnalysisEventKind.Evaluation, AnalysisEventKind.Done,
            },
            events.Select(e => e.Kind).ToArray());

        Assert.Equal(AnalysisStatus.Completed, repository.Saved!.Status);
        Assert.Equal(2, repository.Saved.Requirements.Count);
        Assert.Single(repository.Saved.Requirements[0].Stories);
        Assert.NotNull(repository.Saved.Requirements[0].Stories[0].TestCase);
        Assert.Empty(repository.Saved.Requirements[1].Stories); // no pasó: guardrail, sin historias
        Assert.False(events[6].Evaluation!.Pasa);
    }

    [Fact]
    public async Task AnalyzeAsync_ExtractorFalla_EmiteErrorYPersisteFailed()
    {
        var chat = new StubChatCompletion { Reply = _ => "no json" };
        var repository = new StubRepository();

        var events = await Collect(Orchestrator(chat, repository));

        Assert.Equal(AnalysisEventKind.Error, events[^1].Kind);
        Assert.Equal(AnalysisStatus.Failed, repository.Saved!.Status);
        Assert.NotNull(repository.Saved.Error);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~AnalysisOrchestratorTests"`
Expected: FAIL (no compila).

- [ ] **Step 3: Implementar**

`src/RequirementsCopilot.Application/Analyses/AnalysisEvent.cs`:

```csharp
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public enum AnalysisEventKind { Status, Requirement, Evaluation, Story, TestCase, Done, Error }

public sealed record CriterionDto(string Nombre, int Score, string Observacion);

public sealed record RequirementDto(string Codigo, string Texto, string Area);

public sealed record EvaluationDto(string RequirementCode, IReadOnlyList<CriterionDto> Criterios, double Promedio, double Umbral, bool Pasa);

public sealed record StoryDto(string RequirementCode, int StoryIndex, string Rol, string Quiero, string Para,
    IReadOnlyList<string> CriteriosAceptacion);

public sealed record TestCaseDto(string RequirementCode, int StoryIndex, string Titulo,
    IReadOnlyList<string> Precondiciones, IReadOnlyList<string> Pasos, string ResultadoEsperado);

public sealed record AnalysisEvent(AnalysisEventKind Kind)
{
    public string? Message { get; init; }
    public RequirementDto? Requirement { get; init; }
    public EvaluationDto? Evaluation { get; init; }
    public StoryDto? Story { get; init; }
    public TestCaseDto? TestCase { get; init; }
    public Guid? AnalysisId { get; init; }

    public static AnalysisEvent Status(string message) => new(AnalysisEventKind.Status) { Message = message };

    public static AnalysisEvent FromRequirement(Requirement requirement) => new(AnalysisEventKind.Requirement)
    {
        Requirement = new RequirementDto(requirement.Code, requirement.Text, requirement.Area),
    };

    public static AnalysisEvent FromEvaluation(Requirement requirement) => new(AnalysisEventKind.Evaluation)
    {
        Evaluation = new EvaluationDto(
            requirement.Code,
            requirement.Evaluation!.Scores.Select(s => new CriterionDto(s.Criterion, s.Score, s.Observation)).ToArray(),
            Math.Round(requirement.Evaluation.Average, 2),
            requirement.Evaluation.Threshold,
            requirement.Evaluation.Passed),
    };

    public static AnalysisEvent FromStory(string requirementCode, int storyIndex, UserStory story) => new(AnalysisEventKind.Story)
    {
        Story = new StoryDto(requirementCode, storyIndex, story.Role, story.Goal, story.Benefit, story.AcceptanceCriteria),
    };

    public static AnalysisEvent FromTestCase(string requirementCode, int storyIndex, TestCase testCase) => new(AnalysisEventKind.TestCase)
    {
        TestCase = new TestCaseDto(requirementCode, storyIndex, testCase.Title, testCase.Preconditions,
            testCase.Steps, testCase.ExpectedResult),
    };

    public static AnalysisEvent Done(Guid analysisId) => new(AnalysisEventKind.Done) { AnalysisId = analysisId };

    public static AnalysisEvent Error(string message) => new(AnalysisEventKind.Error) { Message = message };
}
```

`src/RequirementsCopilot.Application/Analyses/AnalysisOrchestrator.cs`:

```csharp
using System.Runtime.CompilerServices;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public sealed class AnalysisOrchestrator
{
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly RequirementExtractorAgent _extractor;
    private readonly RequirementEvaluatorAgent _evaluator;
    private readonly StoryWriterAgent _storyWriter;
    private readonly TestCaseWriterAgent _testCaseWriter;
    private readonly IAnalysisRepository _repository;
    private readonly AnalysisOptions _options;

    public AnalysisOrchestrator(IDocumentTextExtractor textExtractor, RequirementExtractorAgent extractor,
        RequirementEvaluatorAgent evaluator, StoryWriterAgent storyWriter, TestCaseWriterAgent testCaseWriter,
        IAnalysisRepository repository, AnalysisOptions options)
    {
        _textExtractor = textExtractor;
        _extractor = extractor;
        _evaluator = evaluator;
        _storyWriter = storyWriter;
        _testCaseWriter = testCaseWriter;
        _repository = repository;
        _options = options;
    }

    public async IAsyncEnumerable<AnalysisEvent> AnalyzeAsync(Stream content, string fileName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Analysis analysis = Analysis.Create(fileName);
        yield return AnalysisEvent.Status("Extrayendo requerimientos del documento…");

        string? error = null;
        IReadOnlyList<Requirement> requirements = Array.Empty<Requirement>();
        try
        {
            string text = await _textExtractor.ExtractAsync(content, fileName, cancellationToken);
            requirements = await _extractor.ExtractAsync(text, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { error = ex.Message; }

        if (error is null)
        {
            foreach (Requirement requirement in requirements)
            {
                analysis.AddRequirement(requirement);
                yield return AnalysisEvent.FromRequirement(requirement);

                Evaluation? evaluation = null;
                try
                {
                    evaluation = await _evaluator.EvaluateAsync(requirement, _options.PassThreshold, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { error = ex.Message; }
                if (error is not null)
                {
                    break;
                }

                requirement.Evaluate(evaluation!);
                yield return AnalysisEvent.FromEvaluation(requirement);
                if (!evaluation!.Passed)
                {
                    continue; // guardrail: sin historias para requerimientos que no pasan
                }

                IReadOnlyList<UserStory> stories = Array.Empty<UserStory>();
                try
                {
                    stories = await _storyWriter.WriteAsync(requirement, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { error = ex.Message; }
                if (error is not null)
                {
                    break;
                }

                for (int index = 0; index < stories.Count; index++)
                {
                    UserStory story = stories[index];
                    requirement.AddStory(story);
                    yield return AnalysisEvent.FromStory(requirement.Code, index, story);

                    TestCase? testCase = null;
                    try
                    {
                        testCase = await _testCaseWriter.WriteAsync(story, cancellationToken);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { error = ex.Message; }
                    if (error is not null)
                    {
                        break;
                    }

                    story.AttachTestCase(testCase!);
                    yield return AnalysisEvent.FromTestCase(requirement.Code, index, testCase!);
                }

                if (error is not null)
                {
                    break;
                }
            }
        }

        if (error is not null)
        {
            analysis.Fail(error);
            await _repository.SaveAsync(analysis, cancellationToken);
            yield return AnalysisEvent.Error(error);
            yield break;
        }

        analysis.Complete();
        await _repository.SaveAsync(analysis, cancellationToken);
        yield return AnalysisEvent.Done(analysis.Id);
    }
}
```

- [ ] **Step 4: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~AnalysisOrchestratorTests"`
Expected: PASS (2 tests). Luego `dotnet test` completo: PASS (todos).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: orquestador del pipeline de análisis con eventos"
```

---

### Task 9: FakeChatCompletion (demo sin credenciales)

**Files:**
- Create: `src/RequirementsCopilot.Infrastructure/Chat/FakeChatCompletion.cs`
- Test: `tests/RequirementsCopilot.Tests/Infrastructure/FakeChatCompletionTests.cs`

**Interfaces:**
- Consumes: `IChatCompletion`, nombres `AgentName` de los 4 agentes.
- Produces: `class FakeChatCompletion : IChatCompletion` — devuelve JSON canned coherente por agente, para desarrollo/demo a $0. Determinista salvo el evaluador, que alterna: input con código impar (REQ-001, REQ-003…) → rúbrica alta; par → baja (así la demo muestra ambos veredictos).

- [ ] **Step 1: Test que falla** — `tests/RequirementsCopilot.Tests/Infrastructure/FakeChatCompletionTests.cs`:

```csharp
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Infrastructure.Chat;

namespace RequirementsCopilot.Tests.Infrastructure;

public class FakeChatCompletionTests
{
    private readonly FakeChatCompletion _fake = new();

    [Theory]
    [InlineData(RequirementExtractorAgent.AgentName)]
    [InlineData(RequirementEvaluatorAgent.AgentName)]
    [InlineData(StoryWriterAgent.AgentName)]
    [InlineData(TestCaseWriterAgent.AgentName)]
    public async Task CompleteAsync_CadaAgente_DevuelveJsonParseable(string agent)
    {
        var result = await _fake.CompleteAsync(new ChatPrompt(agent, "instr", "REQ-001 input"));
        Assert.NotNull(JsonText.FirstJsonObject(result.Text));
    }

    [Fact]
    public async Task CompleteAsync_PipelineCompletoConAgentesReales_Funciona()
    {
        var extractor = new RequirementExtractorAgent(_fake);
        var requirements = await extractor.ExtractAsync("cualquier documento");
        Assert.True(requirements.Count >= 2);

        var evaluator = new RequirementEvaluatorAgent(_fake);
        var first = await evaluator.EvaluateAsync(requirements[0], 3.5);
        var second = await evaluator.EvaluateAsync(requirements[1], 3.5);
        Assert.True(first.Passed);
        Assert.False(second.Passed);

        var stories = await new StoryWriterAgent(_fake).WriteAsync(requirements[0]);
        Assert.NotEmpty(stories);
        var testCase = await new TestCaseWriterAgent(_fake).WriteAsync(stories[0]);
        Assert.False(string.IsNullOrWhiteSpace(testCase.Title));
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~FakeChatCompletionTests"`
Expected: FAIL (no compila).

- [ ] **Step 3: Implementar** — `src/RequirementsCopilot.Infrastructure/Chat/FakeChatCompletion.cs`:

```csharp
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Analyses.Agents;

namespace RequirementsCopilot.Infrastructure.Chat;

public sealed class FakeChatCompletion : IChatCompletion
{
    private const string ExtractorReply =
        "{\"requerimientos\":[" +
        "{\"codigo\":\"REQ-001\",\"texto\":\"El sistema debe permitir registrar el pago de una reserva con tarjeta, registrando monto, fecha y consecutivo.\",\"area\":\"Pagos\"}," +
        "{\"codigo\":\"REQ-002\",\"texto\":\"El sistema debe ser rápido y fácil de usar.\",\"area\":\"General\"}," +
        "{\"codigo\":\"REQ-003\",\"texto\":\"El sistema debe generar un reporte mensual de ocupación por tipo de habitación en formato Excel.\",\"area\":\"Reportes\"}]}";

    private const string HighRubric =
        "{\"criterios\":[" +
        "{\"nombre\":\"Claridad\",\"score\":5,\"observacion\":\"Redacción precisa y sin ambigüedad.\"}," +
        "{\"nombre\":\"Completitud\",\"score\":4,\"observacion\":\"Define datos y flujo principal.\"}," +
        "{\"nombre\":\"Verificabilidad\",\"score\":5,\"observacion\":\"Resultado observable y medible.\"}," +
        "{\"nombre\":\"Consistencia\",\"score\":4,\"observacion\":\"No contradice otros requerimientos.\"}," +
        "{\"nombre\":\"Factibilidad\",\"score\":5,\"observacion\":\"Implementable con el stack actual.\"}]}";

    private const string LowRubric =
        "{\"criterios\":[" +
        "{\"nombre\":\"Claridad\",\"score\":2,\"observacion\":\"\\\"Rápido\\\" y \\\"fácil\\\" son subjetivos.\"}," +
        "{\"nombre\":\"Completitud\",\"score\":2,\"observacion\":\"No define métricas ni alcance.\"}," +
        "{\"nombre\":\"Verificabilidad\",\"score\":1,\"observacion\":\"Sin umbral no se puede probar.\"}," +
        "{\"nombre\":\"Consistencia\",\"score\":3,\"observacion\":\"No contradice, pero tampoco aporta.\"}," +
        "{\"nombre\":\"Factibilidad\",\"score\":2,\"observacion\":\"Inverificable tal como está escrito.\"}]}";

    private const string StoriesReply =
        "{\"historias\":[{\"rol\":\"recepcionista\",\"quiero\":\"registrar el pago de una reserva\",\"para\":\"confirmar la ocupación\"," +
        "\"criteriosAceptacion\":[\"Dado un monto válido, cuando registro el pago, entonces se genera consecutivo\"," +
        "\"Dado un pago registrado, cuando consulto la reserva, entonces aparece como pagada\"]}]}";

    private const string TestCaseReply =
        "{\"titulo\":\"Registro de pago exitoso\",\"precondiciones\":[\"Reserva creada\",\"Sesión de recepción activa\"]," +
        "\"pasos\":[\"Abrir la reserva\",\"Ingresar monto y tarjeta\",\"Confirmar el pago\"]," +
        "\"resultadoEsperado\":\"El pago queda registrado con consecutivo y la reserva marcada como pagada\"}";

    public Task<ChatResult> CompleteAsync(ChatPrompt prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        string text = prompt.Agent switch
        {
            RequirementExtractorAgent.AgentName => ExtractorReply,
            RequirementEvaluatorAgent.AgentName => IsOddRequirement(prompt.Input) ? HighRubric : LowRubric,
            StoryWriterAgent.AgentName => StoriesReply,
            TestCaseWriterAgent.AgentName => TestCaseReply,
            _ => "{}",
        };
        return Task.FromResult(new ChatResult(text));
    }

    private static bool IsOddRequirement(string input)
    {
        int index = input.IndexOf("REQ-", StringComparison.Ordinal);
        if (index < 0 || index + 7 > input.Length || !int.TryParse(input.Substring(index + 4, 3), out int number))
        {
            return true;
        }
        return number % 2 == 1;
    }
}
```

- [ ] **Step 4: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~FakeChatCompletionTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: adaptador Fake de chat para demo sin credenciales"
```

---

### Task 10: FoundryChatCompletion

**Files:**
- Create: `src/RequirementsCopilot.Infrastructure/Chat/FoundryOptions.cs`, `src/RequirementsCopilot.Infrastructure/Chat/FoundryChatCompletion.cs`
- Test: `tests/RequirementsCopilot.Tests/Infrastructure/FoundryChatCompletionTests.cs`

**Interfaces:**
- Consumes: `IChatCompletion`, `ChatPrompt`, `ChatResult`.
- Produces:
  - `record FoundryOptions { string Endpoint; string ApiKey; string Deployment; string ApiVersion = "2024-10-21" }`
  - `class FoundryChatCompletion : IChatCompletion { ctor(HttpClient, FoundryOptions) }` — POST `{Endpoint}/openai/deployments/{Deployment}/chat/completions?api-version={ApiVersion}` con header `api-key`, body `{ messages: [{role:"system",content:Instructions},{role:"user",content:Input}], temperature: 0.2 }`, lee `choices[0].message.content`.
- **Desviación de JYDE (documentar en ADR, Task 14):** JYDE publica agentes en Foundry (`agent_reference`, Responses API) y las instrucciones viven allá; aquí las instrucciones viven en el código y se usa la Chat Completions API estándar — proyecto autocontenido, sin setup del portal de Foundry por agente.

- [ ] **Step 1: Test que falla** — `tests/RequirementsCopilot.Tests/Infrastructure/FoundryChatCompletionTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Infrastructure.Chat;

namespace RequirementsCopilot.Tests.Infrastructure;

public class FoundryChatCompletionTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }
        public string ResponseBody { get; set; } = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static FoundryOptions Options() => new()
    {
        Endpoint = "https://demo.openai.azure.com",
        ApiKey = "test-key",
        Deployment = "gpt-4.1-mini",
    };

    [Fact]
    public async Task CompleteAsync_ArmaRequestYExtraeContenido()
    {
        var handler = new RecordingHandler
        {
            ResponseBody = "{\"choices\":[{\"message\":{\"content\":\"{\\\"ok\\\":true}\"}}]}",
        };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options());

        var result = await chat.CompleteAsync(new ChatPrompt("agente-x", "instrucciones del agente", "entrada del usuario"));

        Assert.Equal("{\"ok\":true}", result.Text);
        Assert.Equal(
            "https://demo.openai.azure.com/openai/deployments/gpt-4.1-mini/chat/completions?api-version=2024-10-21",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal("test-key", handler.Request.Headers.GetValues("api-key").Single());

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("instrucciones del agente", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
    }

    [Fact]
    public async Task CompleteAsync_RespuestaSinChoices_DevuelveVacio()
    {
        var handler = new RecordingHandler { ResponseBody = "{}" };
        var chat = new FoundryChatCompletion(new HttpClient(handler), Options());
        var result = await chat.CompleteAsync(new ChatPrompt("a", "i", "in"));
        Assert.Equal(string.Empty, result.Text);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~FoundryChatCompletionTests"`
Expected: FAIL (no compila).

- [ ] **Step 3: Implementar**

`src/RequirementsCopilot.Infrastructure/Chat/FoundryOptions.cs`:

```csharp
namespace RequirementsCopilot.Infrastructure.Chat;

public sealed record FoundryOptions
{
    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string Deployment { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = "2024-10-21";
}
```

`src/RequirementsCopilot.Infrastructure/Chat/FoundryChatCompletion.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Chat;

public sealed class FoundryChatCompletion : IChatCompletion
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly FoundryOptions _options;

    public FoundryChatCompletion(HttpClient httpClient, FoundryOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _options = options;
        if (!string.IsNullOrWhiteSpace(options.ApiKey) && !_httpClient.DefaultRequestHeaders.Contains("api-key"))
        {
            _httpClient.DefaultRequestHeaders.Add("api-key", options.ApiKey);
        }
    }

    public async Task<ChatResult> CompleteAsync(ChatPrompt prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var payload = new
        {
            messages = new object[]
            {
                new { role = "system", content = prompt.Instructions },
                new { role = "user", content = prompt.Input },
            },
            temperature = 0.2,
        };

        Uri url = new(
            $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/{_options.Deployment}/chat/completions?api-version={_options.ApiVersion}");
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        return new ChatResult(ExtractContent(document.RootElement));
    }

    private static string ExtractContent(JsonElement root)
    {
        if (root.TryGetProperty("choices", out JsonElement choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out JsonElement message) &&
            message.TryGetProperty("content", out JsonElement content))
        {
            return content.GetString() ?? string.Empty;
        }
        return string.Empty;
    }
}
```

- [ ] **Step 4: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~FoundryChatCompletionTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: adaptador Foundry (Azure OpenAI chat completions)"
```

---

### Task 11: Extractores de texto de documentos

**Files:**
- Create: `src/RequirementsCopilot.Infrastructure/Documents/PlainTextExtractor.cs`, `PdfTextExtractor.cs`, `DocxTextExtractor.cs`, `CompositeTextExtractor.cs`
- Test: `tests/RequirementsCopilot.Tests/Infrastructure/CompositeTextExtractorTests.cs`

**Interfaces:**
- Consumes: `IDocumentTextExtractor`.
- Produces: `class CompositeTextExtractor : IDocumentTextExtractor` — despacha por extensión (`.txt`/`.md` → plano, `.pdf` → PdfPig, `.docx` → OpenXML); extensión no soportada → `NotSupportedException("Formato no soportado: {ext}. Use PDF, DOCX, TXT o MD.")`. Los tres extractores concretos también implementan `IDocumentTextExtractor`.

- [ ] **Step 1: Paquetes**

```powershell
dotnet add src/RequirementsCopilot.Infrastructure package PdfPig --version 0.1.9
dotnet add src/RequirementsCopilot.Infrastructure package DocumentFormat.OpenXml --version 3.2.0
```

- [ ] **Step 2: Test que falla** — `tests/RequirementsCopilot.Tests/Infrastructure/CompositeTextExtractorTests.cs`:

```csharp
using System.Text;
using RequirementsCopilot.Infrastructure.Documents;

namespace RequirementsCopilot.Tests.Infrastructure;

public class CompositeTextExtractorTests
{
    private readonly CompositeTextExtractor _extractor = new();

    [Theory]
    [InlineData("spec.txt")]
    [InlineData("spec.md")]
    [InlineData("SPEC.TXT")]
    public async Task ExtractAsync_TextoPlano_DevuelveContenido(string fileName)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("REQ-001: el sistema debe X"));
        Assert.Equal("REQ-001: el sistema debe X", await _extractor.ExtractAsync(stream, fileName));
    }

    [Fact]
    public async Task ExtractAsync_ExtensionNoSoportada_Lanza()
    {
        using var stream = new MemoryStream(new byte[] { 1 });
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => _extractor.ExtractAsync(stream, "spec.xlsx"));
        Assert.Contains(".xlsx", ex.Message);
    }

    [Fact]
    public async Task ExtractAsync_Docx_ExtraeParrafos()
    {
        using var docx = BuildDocx("El sistema debe registrar pagos.");
        string text = await _extractor.ExtractAsync(docx, "spec.docx");
        Assert.Contains("El sistema debe registrar pagos.", text);
    }

    private static MemoryStream BuildDocx(string paragraph)
    {
        var stream = new MemoryStream();
        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                new DocumentFormat.OpenXml.Wordprocessing.Body(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                            new DocumentFormat.OpenXml.Wordprocessing.Text(paragraph)))));
        }
        stream.Position = 0;
        return stream;
    }
}
```

(PDF no se testea unitariamente — generar un PDF válido en test no lo amerita; PdfPig se valida en la prueba end-to-end de la Task 16.)

- [ ] **Step 3: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~CompositeTextExtractorTests"`
Expected: FAIL (no compila).

- [ ] **Step 4: Implementar**

`src/RequirementsCopilot.Infrastructure/Documents/PlainTextExtractor.cs`:

```csharp
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class PlainTextExtractor : IDocumentTextExtractor
{
    public async Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
```

`src/RequirementsCopilot.Infrastructure/Documents/PdfTextExtractor.cs`:

```csharp
using System.Text;
using RequirementsCopilot.Application.Analyses;
using UglyToad.PdfPig;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class PdfTextExtractor : IDocumentTextExtractor
{
    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        using (var document = PdfDocument.Open(content))
        {
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.AppendLine(page.Text);
            }
        }
        return Task.FromResult(builder.ToString());
    }
}
```

`src/RequirementsCopilot.Infrastructure/Documents/DocxTextExtractor.cs`:

```csharp
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class DocxTextExtractor : IDocumentTextExtractor
{
    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        using (var document = WordprocessingDocument.Open(content, isEditable: false))
        {
            Body? body = document.MainDocumentPart?.Document.Body;
            if (body is not null)
            {
                foreach (Paragraph paragraph in body.Descendants<Paragraph>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    builder.AppendLine(paragraph.InnerText);
                }
            }
        }
        return Task.FromResult(builder.ToString());
    }
}
```

`src/RequirementsCopilot.Infrastructure/Documents/CompositeTextExtractor.cs`:

```csharp
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Infrastructure.Documents;

public sealed class CompositeTextExtractor : IDocumentTextExtractor
{
    private readonly PlainTextExtractor _plain = new();
    private readonly PdfTextExtractor _pdf = new();
    private readonly DocxTextExtractor _docx = new();

    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        IDocumentTextExtractor extractor = extension switch
        {
            ".txt" or ".md" => _plain,
            ".pdf" => _pdf,
            ".docx" => _docx,
            _ => throw new NotSupportedException($"Formato no soportado: {extension}. Use PDF, DOCX, TXT o MD."),
        };
        return extractor.ExtractAsync(content, fileName, cancellationToken);
    }
}
```

- [ ] **Step 5: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~CompositeTextExtractorTests"`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat: extractores de texto PDF, DOCX y plano"
```

---

### Task 12: MongoAnalysisRepository

**Files:**
- Create: `src/RequirementsCopilot.Infrastructure/Mongo/MongoOptions.cs`, `AnalysisDocument.cs`, `MongoAnalysisRepository.cs`
- Test: `tests/RequirementsCopilot.Tests/Infrastructure/AnalysisDocumentTests.cs`

**Interfaces:**
- Consumes: `IAnalysisRepository`, dominio (`Analysis.Rehydrate`, factories).
- Produces:
  - `record MongoOptions { string ConnectionString; string Database = "requirements_copilot" }`
  - `class AnalysisDocument` — DTO de persistencia con mapeos estáticos `FromDomain(Analysis)` / `ToDomain()` (el dominio no se serializa directo a Mongo).
  - `class MongoAnalysisRepository : IAnalysisRepository { ctor(IMongoDatabase) }` — colección `analyses`, `SaveAsync` = `ReplaceOneAsync(_id, upsert)`; `GetAllAsync` ordenado por `CreatedAt` desc.
- El mapeo round-trip se testea sin Mongo; el repositorio es adaptador fino validado en la prueba end-to-end.

- [ ] **Step 1: Paquete**

```powershell
dotnet add src/RequirementsCopilot.Infrastructure package MongoDB.Driver --version 3.1.0
```

- [ ] **Step 2: Test que falla** — `tests/RequirementsCopilot.Tests/Infrastructure/AnalysisDocumentTests.cs`:

```csharp
using RequirementsCopilot.Domain.Analyses;
using RequirementsCopilot.Infrastructure.Mongo;

namespace RequirementsCopilot.Tests.Infrastructure;

public class AnalysisDocumentTests
{
    [Fact]
    public void FromDomain_ToDomain_RoundTripCompleto()
    {
        var analysis = Analysis.Create("spec.pdf");
        var requirement = Requirement.Create("REQ-001", "El sistema debe X", "Pagos");
        requirement.Evaluate(Evaluation.Create(
            new[] { CriterionScore.Create("Claridad", 4, "clara"), CriterionScore.Create("Completitud", 3, "parcial") }, 3.5));
        var story = UserStory.Create("cajero", "registrar pago", "cerrar venta", new[] { "dado A entonces B" });
        story.AttachTestCase(TestCase.Create("Pago ok", new[] { "caja abierta" }, new[] { "registrar" }, "registrado"));
        requirement.AddStory(story);
        analysis.AddRequirement(requirement);
        analysis.Complete();

        var restored = AnalysisDocument.FromDomain(analysis).ToDomain();

        Assert.Equal(analysis.Id, restored.Id);
        Assert.Equal(analysis.Status, restored.Status);
        Assert.Equal("REQ-001", restored.Requirements[0].Code);
        Assert.Equal("Pagos", restored.Requirements[0].Area);
        Assert.Equal(3.5, restored.Requirements[0].Evaluation!.Average);
        Assert.True(restored.Requirements[0].Evaluation!.Passed);
        Assert.Equal("Pago ok", restored.Requirements[0].Stories[0].TestCase!.Title);
    }

    [Fact]
    public void FromDomain_ToDomain_AnalisisFallido()
    {
        var analysis = Analysis.Create("spec.txt");
        analysis.Fail("LLM no disponible");
        var restored = AnalysisDocument.FromDomain(analysis).ToDomain();
        Assert.Equal(AnalysisStatus.Failed, restored.Status);
        Assert.Equal("LLM no disponible", restored.Error);
    }
}
```

- [ ] **Step 3: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~AnalysisDocumentTests"`
Expected: FAIL (no compila).

- [ ] **Step 4: Implementar**

`src/RequirementsCopilot.Infrastructure/Mongo/MongoOptions.cs`:

```csharp
namespace RequirementsCopilot.Infrastructure.Mongo;

public sealed record MongoOptions
{
    public string ConnectionString { get; init; } = string.Empty;
    public string Database { get; init; } = "requirements_copilot";
}
```

`src/RequirementsCopilot.Infrastructure/Mongo/AnalysisDocument.cs`:

```csharp
using MongoDB.Bson.Serialization.Attributes;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Infrastructure.Mongo;

public sealed class AnalysisDocument
{
    [BsonId]
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public List<RequirementDocument> Requirements { get; set; } = new();

    public static AnalysisDocument FromDomain(Analysis analysis) => new()
    {
        Id = analysis.Id,
        FileName = analysis.FileName,
        CreatedAt = analysis.CreatedAt,
        Status = analysis.Status.ToString(),
        Error = analysis.Error,
        Requirements = analysis.Requirements.Select(RequirementDocument.FromDomain).ToList(),
    };

    public Analysis ToDomain() => Analysis.Rehydrate(
        Id, FileName, CreatedAt, Enum.Parse<AnalysisStatus>(Status), Error,
        Requirements.Select(r => r.ToDomain()).ToArray());
}

public sealed class RequirementDocument
{
    public string Code { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public EvaluationDocument? Evaluation { get; set; }
    public List<StoryDocument> Stories { get; set; } = new();

    public static RequirementDocument FromDomain(Requirement requirement) => new()
    {
        Code = requirement.Code,
        Text = requirement.Text,
        Area = requirement.Area,
        Evaluation = requirement.Evaluation is null ? null : EvaluationDocument.FromDomain(requirement.Evaluation),
        Stories = requirement.Stories.Select(StoryDocument.FromDomain).ToList(),
    };

    public Requirement ToDomain()
    {
        var requirement = Requirement.Create(Code, Text, Area);
        if (Evaluation is not null)
        {
            requirement.Evaluate(Evaluation.ToDomain());
        }
        foreach (StoryDocument story in Stories)
        {
            requirement.AddStory(story.ToDomain());
        }
        return requirement;
    }
}

public sealed class EvaluationDocument
{
    public List<CriterionDocument> Scores { get; set; } = new();
    public double Threshold { get; set; }

    public static EvaluationDocument FromDomain(Evaluation evaluation) => new()
    {
        Scores = evaluation.Scores.Select(s => new CriterionDocument
        {
            Criterion = s.Criterion, Score = s.Score, Observation = s.Observation,
        }).ToList(),
        Threshold = evaluation.Threshold,
    };

    public Evaluation ToDomain() => Evaluation.Create(
        Scores.Select(s => CriterionScore.Create(s.Criterion, s.Score, s.Observation)).ToArray(), Threshold);
}

public sealed class CriterionDocument
{
    public string Criterion { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Observation { get; set; } = string.Empty;
}

public sealed class StoryDocument
{
    public string Role { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string Benefit { get; set; } = string.Empty;
    public List<string> AcceptanceCriteria { get; set; } = new();
    public TestCaseDocument? TestCase { get; set; }

    public static StoryDocument FromDomain(UserStory story) => new()
    {
        Role = story.Role,
        Goal = story.Goal,
        Benefit = story.Benefit,
        AcceptanceCriteria = story.AcceptanceCriteria.ToList(),
        TestCase = story.TestCase is null ? null : TestCaseDocument.FromDomain(story.TestCase),
    };

    public UserStory ToDomain()
    {
        var story = UserStory.Create(Role, Goal, Benefit, AcceptanceCriteria);
        if (TestCase is not null)
        {
            story.AttachTestCase(TestCase.ToDomain());
        }
        return story;
    }
}

public sealed class TestCaseDocument
{
    public string Title { get; set; } = string.Empty;
    public List<string> Preconditions { get; set; } = new();
    public List<string> Steps { get; set; } = new();
    public string ExpectedResult { get; set; } = string.Empty;

    public static TestCaseDocument FromDomain(TestCase testCase) => new()
    {
        Title = testCase.Title,
        Preconditions = testCase.Preconditions.ToList(),
        Steps = testCase.Steps.ToList(),
        ExpectedResult = testCase.ExpectedResult,
    };

    public TestCase ToDomain() => TestCase.Create(Title, Preconditions, Steps, ExpectedResult);
}
```

`src/RequirementsCopilot.Infrastructure/Mongo/MongoAnalysisRepository.cs`:

```csharp
using MongoDB.Driver;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Infrastructure.Mongo;

public sealed class MongoAnalysisRepository : IAnalysisRepository
{
    private readonly IMongoCollection<AnalysisDocument> _collection;

    public MongoAnalysisRepository(IMongoDatabase database)
        => _collection = database.GetCollection<AnalysisDocument>("analyses");

    public Task SaveAsync(Analysis analysis, CancellationToken cancellationToken = default)
        => _collection.ReplaceOneAsync(d => d.Id == analysis.Id, AnalysisDocument.FromDomain(analysis),
            new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public async Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AnalysisDocument? document = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<IReadOnlyList<Analysis>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<AnalysisDocument> documents = await _collection.Find(FilterDefinition<AnalysisDocument>.Empty)
            .SortByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
        return documents.Select(d => d.ToDomain()).ToArray();
    }
}
```

- [ ] **Step 5: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~AnalysisDocumentTests"`
Expected: PASS (2 tests). Luego `dotnet test` completo: PASS.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat: repositorio Mongo de análisis con documento de persistencia"
```

---

### Task 13: Api — consultas, controller SSE y composition root

**Files:**
- Create: `src/RequirementsCopilot.Application/Analyses/AnalysisQueries.cs`, `src/RequirementsCopilot.Infrastructure/Persistence/InMemoryAnalysisRepository.cs`, `src/RequirementsCopilot.Api/Controllers/AnalysesController.cs`
- Modify: `src/RequirementsCopilot.Api/Program.cs`, `src/RequirementsCopilot.Api/appsettings.json`
- Test: `tests/RequirementsCopilot.Tests/Api/AnalysesEndpointTests.cs`

**Interfaces:**
- Consumes: `AnalysisOrchestrator`, `IAnalysisRepository`, adaptadores de Infrastructure, DTOs de Task 8.
- Produces (namespace `RequirementsCopilot.Application.Analyses`):
  - `record TestCaseDetailDto(string Titulo, IReadOnlyList<string> Precondiciones, IReadOnlyList<string> Pasos, string ResultadoEsperado)`
  - `record StoryDetailDto(string Rol, string Quiero, string Para, IReadOnlyList<string> CriteriosAceptacion, TestCaseDetailDto? Caso)`
  - `record RequirementDetailDto(string Codigo, string Texto, string Area, EvaluationDto? Evaluacion, IReadOnlyList<StoryDetailDto> Historias)`
  - `record AnalysisSummaryDto(Guid Id, string FileName, DateTime CreatedAt, string Status, int TotalRequerimientos, int Aprobados)`
  - `record AnalysisDetailDto(Guid Id, string FileName, DateTime CreatedAt, string Status, string? Error, IReadOnlyList<RequirementDetailDto> Requerimientos)`
  - `class AnalysisQueries { ctor(IAnalysisRepository); Task<IReadOnlyList<AnalysisSummaryDto>> GetAllAsync(ct); Task<AnalysisDetailDto?> GetByIdAsync(Guid id, ct); }`
  - `class InMemoryAnalysisRepository : IAnalysisRepository` (ConcurrentDictionary; `Providers:AnalysisRepository = InMemory` para dev/tests sin Mongo).
- Endpoints: `POST /api/analyses` (multipart `file`, SSE), `GET /api/analyses`, `GET /api/analyses/{id}`.
- Eventos SSE (nombre = kind en minúscula): `status {mensaje}`, `requirement {requerimiento}`, `evaluation {evaluacion}`, `story {historia}`, `testcase {caso}`, `done {analysisId}`, `error {mensaje}`.

- [ ] **Step 1: Paquete de test de integración**

```powershell
dotnet add tests/RequirementsCopilot.Tests package Microsoft.AspNetCore.Mvc.Testing --version 8.0.8
dotnet add tests/RequirementsCopilot.Tests reference src/RequirementsCopilot.Api
```

- [ ] **Step 2: Test que falla** — `tests/RequirementsCopilot.Tests/Api/AnalysesEndpointTests.cs`:

```csharp
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
```

- [ ] **Step 3: Verificar que falla**

Run: `dotnet test --filter "FullyQualifiedName~AnalysesEndpointTests"`
Expected: FAIL (no compila / 404).

- [ ] **Step 4: Implementar**

`src/RequirementsCopilot.Application/Analyses/AnalysisQueries.cs`:

```csharp
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses;

public sealed record TestCaseDetailDto(string Titulo, IReadOnlyList<string> Precondiciones, IReadOnlyList<string> Pasos,
    string ResultadoEsperado);

public sealed record StoryDetailDto(string Rol, string Quiero, string Para, IReadOnlyList<string> CriteriosAceptacion,
    TestCaseDetailDto? Caso);

public sealed record RequirementDetailDto(string Codigo, string Texto, string Area, EvaluationDto? Evaluacion,
    IReadOnlyList<StoryDetailDto> Historias);

public sealed record AnalysisSummaryDto(Guid Id, string FileName, DateTime CreatedAt, string Status,
    int TotalRequerimientos, int Aprobados);

public sealed record AnalysisDetailDto(Guid Id, string FileName, DateTime CreatedAt, string Status, string? Error,
    IReadOnlyList<RequirementDetailDto> Requerimientos);

public sealed class AnalysisQueries
{
    private readonly IAnalysisRepository _repository;

    public AnalysisQueries(IAnalysisRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<AnalysisSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Analysis> analyses = await _repository.GetAllAsync(cancellationToken);
        return analyses.Select(a => new AnalysisSummaryDto(
            a.Id, a.FileName, a.CreatedAt, a.Status.ToString(),
            a.Requirements.Count,
            a.Requirements.Count(r => r.Evaluation?.Passed == true))).ToArray();
    }

    public async Task<AnalysisDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Analysis? analysis = await _repository.GetByIdAsync(id, cancellationToken);
        if (analysis is null)
        {
            return null;
        }

        return new AnalysisDetailDto(analysis.Id, analysis.FileName, analysis.CreatedAt, analysis.Status.ToString(),
            analysis.Error,
            analysis.Requirements.Select(r => new RequirementDetailDto(
                r.Code, r.Text, r.Area,
                r.Evaluation is null ? null : new EvaluationDto(
                    r.Code,
                    r.Evaluation.Scores.Select(s => new CriterionDto(s.Criterion, s.Score, s.Observation)).ToArray(),
                    Math.Round(r.Evaluation.Average, 2), r.Evaluation.Threshold, r.Evaluation.Passed),
                r.Stories.Select(s => new StoryDetailDto(
                    s.Role, s.Goal, s.Benefit, s.AcceptanceCriteria,
                    s.TestCase is null ? null : new TestCaseDetailDto(
                        s.TestCase.Title, s.TestCase.Preconditions, s.TestCase.Steps, s.TestCase.ExpectedResult)))
                    .ToArray()))
                .ToArray());
    }
}
```

`src/RequirementsCopilot.Infrastructure/Persistence/InMemoryAnalysisRepository.cs`:

```csharp
using System.Collections.Concurrent;
using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Infrastructure.Persistence;

public sealed class InMemoryAnalysisRepository : IAnalysisRepository
{
    private readonly ConcurrentDictionary<Guid, Analysis> _store = new();

    public Task SaveAsync(Analysis analysis, CancellationToken cancellationToken = default)
    {
        _store[analysis.Id] = analysis;
        return Task.CompletedTask;
    }

    public Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id, out Analysis? analysis) ? analysis : null);

    public Task<IReadOnlyList<Analysis>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Analysis>>(_store.Values.OrderByDescending(a => a.CreatedAt).ToArray());
}
```

`src/RequirementsCopilot.Api/Controllers/AnalysesController.cs`:

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public sealed class AnalysesController : ControllerBase
{
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".txt", ".md" };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private readonly AnalysisOrchestrator _orchestrator;
    private readonly AnalysisQueries _queries;

    public AnalysesController(AnalysisOrchestrator orchestrator, AnalysisQueries queries)
        => (_orchestrator, _queries) = (orchestrator, queries);

    [HttpPost]
    [RequestSizeLimit(MaxFileBytes + 1024)]
    public async Task<IActionResult> Analyze(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { mensaje = "Debe adjuntar un archivo." });
        }
        if (file.Length > MaxFileBytes)
        {
            return BadRequest(new { mensaje = "El archivo supera el máximo de 10 MB." });
        }
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(new { mensaje = $"Formato no soportado: {extension}. Use PDF, DOCX, TXT o MD." });
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        await using Stream content = file.OpenReadStream();
        await foreach (AnalysisEvent analysisEvent in _orchestrator.AnalyzeAsync(content, file.FileName, cancellationToken))
        {
            await WriteEventAsync(analysisEvent, cancellationToken);
        }
        return new EmptyResult();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await _queries.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        AnalysisDetailDto? detail = await _queries.GetByIdAsync(id, cancellationToken);
        return detail is null ? NotFound(new { mensaje = "Análisis no encontrado." }) : Ok(detail);
    }

    private async Task WriteEventAsync(AnalysisEvent analysisEvent, CancellationToken cancellationToken)
    {
        string name = analysisEvent.Kind.ToString().ToLowerInvariant();
        object payload = analysisEvent.Kind switch
        {
            AnalysisEventKind.Status => new { mensaje = analysisEvent.Message },
            AnalysisEventKind.Requirement => new { requerimiento = analysisEvent.Requirement },
            AnalysisEventKind.Evaluation => new { evaluacion = analysisEvent.Evaluation },
            AnalysisEventKind.Story => new { historia = analysisEvent.Story },
            AnalysisEventKind.TestCase => new { caso = analysisEvent.TestCase },
            AnalysisEventKind.Done => new { analysisId = analysisEvent.AnalysisId },
            AnalysisEventKind.Error => new { mensaje = analysisEvent.Message },
            _ => new { },
        };
        string data = JsonSerializer.Serialize(payload, JsonOptions);
        await Response.WriteAsync($"event: {name}\ndata: {data}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
```

`src/RequirementsCopilot.Api/Program.cs` (reemplazar completo):

```csharp
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
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(corsOrigin).AllowAnyHeader().AllowAnyMethod()));

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
```

Nota: `IMongoDatabase` se registra con el tipo concreto devuelto — usar `builder.Services.AddSingleton<MongoDB.Driver.IMongoDatabase>(sp => ...)` explícito para que `MongoAnalysisRepository` resuelva.

`src/RequirementsCopilot.Api/appsettings.json` (reemplazar completo):

```json
{
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "AllowedHosts": "*",
  "Providers": { "Chat": "Fake", "AnalysisRepository": "InMemory" },
  "Analysis": { "PassThreshold": 3.5 },
  "Foundry": { "Endpoint": "", "ApiKey": "", "Deployment": "gpt-4.1-mini" },
  "Mongo": { "ConnectionString": "", "Database": "requirements_copilot" },
  "Cors": { "Origin": "http://localhost:5173" }
}
```

Credenciales reales (Foundry/Mongo) van SOLO en `appsettings.Development.json` (agregar a `.gitignore` la línea `src/RequirementsCopilot.Api/appsettings.Development.json`) o en variables de entorno `Foundry__ApiKey`, etc.

- [ ] **Step 5: Verificar que pasa**

Run: `dotnet test --filter "FullyQualifiedName~AnalysesEndpointTests"`
Expected: PASS (4 tests). Luego `dotnet test` completo: PASS.

- [ ] **Step 6: Smoke manual**

Run: `dotnet run --project src/RequirementsCopilot.Api` y en otra terminal:
`curl -N -F "file=@docs/superpowers/specs/2026-07-14-requirements-copilot-design.md" http://localhost:5000/api/analyses` (puerto real según `launchSettings.json`).
Expected: stream de eventos SSE terminando en `event: done`.

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat: API de análisis con SSE, consultas y composition root"
```

---

### Task 14: Scaffold frontend (Vite + React + TS + Zustand + Tailwind + Vitest)

**Files:**
- Create: `web/` (proyecto Vite), `web/vite.config.ts`, `web/src/styles/index.css`, `web/.env.development`
- Modify: `src/RequirementsCopilot.Api/Properties/launchSettings.json` (puerto fijo 5100)

**Interfaces:**
- Produces: app Vite compilable con Tailwind activo, `npm test` (Vitest) funcionando, backend en puerto fijo `http://localhost:5100`.

- [ ] **Step 1: Fijar puerto del backend**

En `src/RequirementsCopilot.Api/Properties/launchSettings.json`, en el perfil `http`, dejar `"applicationUrl": "http://localhost:5100"`.

- [ ] **Step 2: Scaffold Vite**

```powershell
cd "D:\Repositories\Daniela Benitez\RequirementsCopilot"
npm create vite@latest web -- --template react-ts
cd web
npm install
npm install zustand tailwindcss @tailwindcss/vite
npm install -D vitest
```

- [ ] **Step 3: Configurar Tailwind y Vitest**

`web/vite.config.ts` (reemplazar):

```ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
});
```

Crear `web/src/styles/index.css`:

```css
@import 'tailwindcss';
```

En `web/src/main.tsx` importar `./styles/index.css` (quitar el `./index.css` de la plantilla y borrar `web/src/index.css`, `web/src/App.css`, `web/src/assets/react.svg`).

`web/.env.development`:

```
VITE_API_BASE_URL=http://localhost:5100
```

En `web/package.json`, agregar script: `"test": "vitest run"`.

- [ ] **Step 4: Verificar**

Run: `npm run build` (en `web/`)
Expected: build OK.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "chore: scaffold frontend Vite + Zustand + Tailwind + Vitest"
```

---

### Task 15: Frontend — tipos, parser SSE, cliente API y store Zustand

**Files:**
- Create: `web/src/shared/types.ts`, `web/src/shared/api/sse.ts`, `web/src/shared/api/client.ts`, `web/src/features/analysis/store.ts`
- Test: `web/src/shared/api/sse.test.ts`, `web/src/features/analysis/store.test.ts`

**Interfaces:**
- Produces:
  - `types.ts`: `Criterion {nombre, score, observacion}`, `Evaluacion {requirementCode, criterios, promedio, umbral, pasa}`, `Caso {titulo, precondiciones, pasos, resultadoEsperado}`, `Historia {rol, quiero, para, criteriosAceptacion, caso?}`, `RequirementView {codigo, texto, area, evaluacion?, historias}`, `AnalysisSummary {id, fileName, createdAt, status, totalRequerimientos, aprobados}`, `SseEvent {event: string, data: any}`.
  - `sse.ts`: `async function* parseSse(body: ReadableStream<Uint8Array>): AsyncGenerator<SseEvent>` — bufferiza, separa por `\n\n`, lee líneas `event:`/`data:`, `JSON.parse` de data.
  - `client.ts`: `API_BASE` (de `import.meta.env.VITE_API_BASE_URL`), `analyzeFile(file: File): Promise<Response>` (POST FormData), `getAnalyses()`, `getAnalysis(id)`.
  - `store.ts`: Zustand `useAnalysisStore` — estado `{ status: 'idle'|'running'|'done'|'error', statusMessage, error, analysisId, requirements: RequirementView[] }` + acciones `start()`, `applyEvent(evt: SseEvent)`, `reset()`. `applyEvent` reduce cada evento SSE al árbol de requerimientos (evaluation se asocia por `requirementCode`, story/testcase por `requirementCode` + `storyIndex`).

- [ ] **Step 1: Tests que fallan**

`web/src/shared/api/sse.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { parseSse } from './sse';

function streamOf(...chunks: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  return new ReadableStream({
    start(controller) {
      chunks.forEach((c) => controller.enqueue(encoder.encode(c)));
      controller.close();
    },
  });
}

async function collect(stream: ReadableStream<Uint8Array>) {
  const events = [];
  for await (const evt of parseSse(stream)) events.push(evt);
  return events;
}

describe('parseSse', () => {
  it('parsea eventos completos', async () => {
    const events = await collect(
      streamOf('event: status\ndata: {"mensaje":"hola"}\n\nevent: done\ndata: {"analysisId":"x"}\n\n'),
    );
    expect(events).toEqual([
      { event: 'status', data: { mensaje: 'hola' } },
      { event: 'done', data: { analysisId: 'x' } },
    ]);
  });

  it('re-ensambla eventos partidos entre chunks', async () => {
    const events = await collect(streamOf('event: sta', 'tus\ndata: {"mensaje":"ok"}\n', '\n'));
    expect(events).toEqual([{ event: 'status', data: { mensaje: 'ok' } }]);
  });
});
```

`web/src/features/analysis/store.test.ts`:

```ts
import { beforeEach, describe, expect, it } from 'vitest';
import { useAnalysisStore } from './store';

const req = { codigo: 'REQ-001', texto: 'debe X', area: 'Pagos' };
const evalOk = {
  requirementCode: 'REQ-001',
  criterios: [{ nombre: 'Claridad', score: 5, observacion: 'ok' }],
  promedio: 5,
  umbral: 3.5,
  pasa: true,
};
const historia = {
  requirementCode: 'REQ-001', storyIndex: 0, rol: 'cajero', quiero: 'pagar', para: 'cerrar',
  criteriosAceptacion: ['dado A entonces B'],
};
const caso = {
  requirementCode: 'REQ-001', storyIndex: 0, titulo: 'Pago ok', precondiciones: [], pasos: ['ir'],
  resultadoEsperado: 'pagado',
};

describe('useAnalysisStore.applyEvent', () => {
  beforeEach(() => useAnalysisStore.getState().reset());

  it('arma el árbol requerimiento → evaluación → historia → caso', () => {
    const { applyEvent } = useAnalysisStore.getState();
    applyEvent({ event: 'requirement', data: { requerimiento: req } });
    applyEvent({ event: 'evaluation', data: { evaluacion: evalOk } });
    applyEvent({ event: 'story', data: { historia } });
    applyEvent({ event: 'testcase', data: { caso } });
    applyEvent({ event: 'done', data: { analysisId: 'abc' } });

    const state = useAnalysisStore.getState();
    expect(state.status).toBe('done');
    expect(state.requirements).toHaveLength(1);
    expect(state.requirements[0].evaluacion?.pasa).toBe(true);
    expect(state.requirements[0].historias[0].caso?.titulo).toBe('Pago ok');
  });

  it('error marca el estado y guarda el mensaje', () => {
    const { applyEvent } = useAnalysisStore.getState();
    applyEvent({ event: 'error', data: { mensaje: 'falló el LLM' } });
    const state = useAnalysisStore.getState();
    expect(state.status).toBe('error');
    expect(state.error).toBe('falló el LLM');
  });
});
```

- [ ] **Step 2: Verificar que fallan**

Run (en `web/`): `npm test`
Expected: FAIL (módulos no existen).

- [ ] **Step 3: Implementar**

`web/src/shared/types.ts`:

```ts
export interface Criterion { nombre: string; score: number; observacion: string }
export interface Evaluacion {
  requirementCode: string; criterios: Criterion[]; promedio: number; umbral: number; pasa: boolean;
}
export interface Caso { titulo: string; precondiciones: string[]; pasos: string[]; resultadoEsperado: string }
export interface Historia {
  rol: string; quiero: string; para: string; criteriosAceptacion: string[]; caso?: Caso;
}
export interface RequirementView {
  codigo: string; texto: string; area: string; evaluacion?: Evaluacion; historias: Historia[];
}
export interface AnalysisSummary {
  id: string; fileName: string; createdAt: string; status: string;
  totalRequerimientos: number; aprobados: number;
}
export interface SseEvent { event: string; data: any }
```

`web/src/shared/api/sse.ts`:

```ts
import type { SseEvent } from '../types';

export async function* parseSse(body: ReadableStream<Uint8Array>): AsyncGenerator<SseEvent> {
  const reader = body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    let separator: number;
    while ((separator = buffer.indexOf('\n\n')) >= 0) {
      const block = buffer.slice(0, separator);
      buffer = buffer.slice(separator + 2);
      const event = parseBlock(block);
      if (event) yield event;
    }
  }
}

function parseBlock(block: string): SseEvent | null {
  let name = '';
  let data = '';
  for (const line of block.split('\n')) {
    if (line.startsWith('event: ')) name = line.slice(7).trim();
    else if (line.startsWith('data: ')) data += line.slice(6);
  }
  if (!name || !data) return null;
  try {
    return { event: name, data: JSON.parse(data) };
  } catch {
    return null;
  }
}
```

`web/src/shared/api/client.ts`:

```ts
import type { AnalysisSummary } from '../types';

export const API_BASE: string = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5100';

export async function analyzeFile(file: File): Promise<Response> {
  const form = new FormData();
  form.append('file', file);
  const response = await fetch(`${API_BASE}/api/analyses`, { method: 'POST', body: form });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.mensaje ?? `Error ${response.status}`);
  }
  return response;
}

export async function getAnalyses(): Promise<AnalysisSummary[]> {
  const response = await fetch(`${API_BASE}/api/analyses`);
  if (!response.ok) throw new Error('No se pudo cargar el historial.');
  return response.json();
}

export async function getAnalysis(id: string): Promise<any> {
  const response = await fetch(`${API_BASE}/api/analyses/${id}`);
  if (!response.ok) throw new Error('No se pudo cargar el análisis.');
  return response.json();
}
```

`web/src/features/analysis/store.ts`:

```ts
import { create } from 'zustand';
import type { RequirementView, SseEvent } from '../../shared/types';

interface AnalysisState {
  status: 'idle' | 'running' | 'done' | 'error';
  statusMessage: string;
  error?: string;
  analysisId?: string;
  requirements: RequirementView[];
  start: () => void;
  applyEvent: (evt: SseEvent) => void;
  reset: () => void;
}

const initial = {
  status: 'idle' as const,
  statusMessage: '',
  error: undefined,
  analysisId: undefined,
  requirements: [] as RequirementView[],
};

export const useAnalysisStore = create<AnalysisState>((set) => ({
  ...initial,
  start: () => set({ ...initial, status: 'running' }),
  reset: () => set({ ...initial }),
  applyEvent: (evt) =>
    set((state) => {
      switch (evt.event) {
        case 'status':
          return { statusMessage: evt.data.mensaje ?? '' };
        case 'requirement':
          return {
            requirements: [...state.requirements, { ...evt.data.requerimiento, historias: [] }],
          };
        case 'evaluation':
          return {
            requirements: state.requirements.map((r) =>
              r.codigo === evt.data.evaluacion.requirementCode ? { ...r, evaluacion: evt.data.evaluacion } : r,
            ),
          };
        case 'story': {
          const { requirementCode, storyIndex, ...historia } = evt.data.historia;
          return {
            requirements: state.requirements.map((r) => {
              if (r.codigo !== requirementCode) return r;
              const historias = [...r.historias];
              historias[storyIndex] = { ...historia };
              return { ...r, historias };
            }),
          };
        }
        case 'testcase': {
          const { requirementCode, storyIndex, ...caso } = evt.data.caso;
          return {
            requirements: state.requirements.map((r) => {
              if (r.codigo !== requirementCode) return r;
              const historias = r.historias.map((h, i) => (i === storyIndex ? { ...h, caso } : h));
              return { ...r, historias };
            }),
          };
        }
        case 'done':
          return { status: 'done', analysisId: evt.data.analysisId, statusMessage: '' };
        case 'error':
          return { status: 'error', error: evt.data.mensaje ?? 'Error desconocido' };
        default:
          return {};
      }
    }),
}));
```

- [ ] **Step 4: Verificar que pasan**

Run (en `web/`): `npm test`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat: parser SSE, cliente API y store Zustand del análisis"
```

---

### Task 16: Frontend — vistas Analizar, Historial y Detalle

**Files:**
- Create: `web/src/features/analysis/AnalyzeView.tsx`, `web/src/features/analysis/RequirementCard.tsx`, `web/src/features/history/HistoryView.tsx`, `web/src/features/history/DetailView.tsx`
- Modify: `web/src/App.tsx`, `web/src/main.tsx`

**Interfaces:**
- Consumes: store, cliente API, `parseSse`, tipos (Task 15).
- Produces: SPA con 2 pestañas (Analizar | Historial) y detalle con **filtro por área** (chips). Sin router — el estado de navegación vive en `App` (YAGNI: 3 vistas no ameritan react-router).

- [ ] **Step 1: Implementar componentes**

`web/src/features/analysis/RequirementCard.tsx`:

```tsx
import type { RequirementView } from '../../shared/types';

export function RequirementCard({ requirement }: { requirement: RequirementView }) {
  const evaluacion = requirement.evaluacion;
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-center gap-2">
        <span className="font-mono text-sm font-bold text-slate-700">{requirement.codigo}</span>
        <span className="rounded-full bg-indigo-100 px-2 py-0.5 text-xs text-indigo-700">{requirement.area}</span>
        {evaluacion && (
          <span
            className={`ml-auto rounded-full px-2 py-0.5 text-xs font-semibold ${
              evaluacion.pasa ? 'bg-emerald-100 text-emerald-700' : 'bg-rose-100 text-rose-700'
            }`}
          >
            {evaluacion.pasa ? 'Aprobado' : 'No aprobado'} · {evaluacion.promedio.toFixed(2)} / {evaluacion.umbral}
          </span>
        )}
      </div>
      <p className="mt-2 text-sm text-slate-700">{requirement.texto}</p>

      {evaluacion && (
        <table className="mt-3 w-full text-xs">
          <tbody>
            {evaluacion.criterios.map((c) => (
              <tr key={c.nombre} className="border-t border-slate-100">
                <td className="py-1 font-medium text-slate-600">{c.nombre}</td>
                <td className="py-1 text-center font-mono">{c.score}/5</td>
                <td className="py-1 text-slate-500">{c.observacion}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {requirement.historias.map((h, i) => (
        <div key={i} className="mt-3 rounded-lg bg-slate-50 p-3">
          <p className="text-sm">
            <span className="font-semibold">Como</span> {h.rol}, <span className="font-semibold">quiero</span>{' '}
            {h.quiero}, <span className="font-semibold">para</span> {h.para}.
          </p>
          <ul className="mt-1 list-disc pl-5 text-xs text-slate-600">
            {h.criteriosAceptacion.map((c, j) => (
              <li key={j}>{c}</li>
            ))}
          </ul>
          {h.caso && (
            <div className="mt-2 rounded border border-slate-200 bg-white p-2 text-xs">
              <p className="font-semibold text-slate-700">Caso de prueba: {h.caso.titulo}</p>
              {h.caso.precondiciones.length > 0 && <p className="mt-1">Precondiciones: {h.caso.precondiciones.join('; ')}</p>}
              <ol className="mt-1 list-decimal pl-5">
                {h.caso.pasos.map((p, j) => (
                  <li key={j}>{p}</li>
                ))}
              </ol>
              <p className="mt-1 text-emerald-700">Resultado esperado: {h.caso.resultadoEsperado}</p>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
```

`web/src/features/analysis/AnalyzeView.tsx`:

```tsx
import { useRef, useState } from 'react';
import { analyzeFile } from '../../shared/api/client';
import { parseSse } from '../../shared/api/sse';
import { useAnalysisStore } from './store';
import { RequirementCard } from './RequirementCard';

export function AnalyzeView() {
  const { status, statusMessage, error, requirements, start, applyEvent } = useAnalysisStore();
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);

  async function analyze(file: File) {
    start();
    try {
      const response = await analyzeFile(file);
      for await (const evt of parseSse(response.body!)) applyEvent(evt);
    } catch (e) {
      applyEvent({ event: 'error', data: { mensaje: e instanceof Error ? e.message : 'Error inesperado' } });
    }
  }

  return (
    <div className="space-y-4">
      <div
        className={`flex cursor-pointer flex-col items-center rounded-xl border-2 border-dashed p-8 text-slate-500 ${
          dragging ? 'border-indigo-400 bg-indigo-50' : 'border-slate-300'
        }`}
        onClick={() => inputRef.current?.click()}
        onDragOver={(e) => {
          e.preventDefault();
          setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          const file = e.dataTransfer.files[0];
          if (file) void analyze(file);
        }}
      >
        <p className="font-medium">Arrastra un documento o haz clic para seleccionarlo</p>
        <p className="mt-1 text-xs">PDF, DOCX, TXT o MD · máximo 10 MB</p>
        <input
          ref={inputRef}
          type="file"
          accept=".pdf,.docx,.txt,.md"
          className="hidden"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) void analyze(file);
            e.target.value = '';
          }}
        />
      </div>

      {status === 'running' && (
        <p className="animate-pulse text-sm text-indigo-600">{statusMessage || 'Analizando…'}</p>
      )}
      {status === 'error' && <p className="rounded bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}
      {status === 'done' && <p className="text-sm text-emerald-700">Análisis completado.</p>}

      <div className="space-y-3">
        {requirements.map((r) => (
          <RequirementCard key={r.codigo} requirement={r} />
        ))}
      </div>
    </div>
  );
}
```

`web/src/features/history/HistoryView.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { getAnalyses } from '../../shared/api/client';
import type { AnalysisSummary } from '../../shared/types';

export function HistoryView({ onSelect }: { onSelect: (id: string) => void }) {
  const [items, setItems] = useState<AnalysisSummary[]>([]);
  const [error, setError] = useState<string>();

  useEffect(() => {
    getAnalyses().then(setItems).catch((e) => setError(e.message));
  }, []);

  if (error) return <p className="rounded bg-rose-50 p-3 text-sm text-rose-700">{error}</p>;
  if (items.length === 0) return <p className="text-sm text-slate-500">Sin análisis todavía.</p>;

  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="border-b border-slate-200 text-left text-xs uppercase text-slate-500">
          <th className="py-2">Archivo</th>
          <th>Fecha</th>
          <th>Estado</th>
          <th>Requerimientos</th>
          <th>Aprobados</th>
        </tr>
      </thead>
      <tbody>
        {items.map((a) => (
          <tr
            key={a.id}
            className="cursor-pointer border-b border-slate-100 hover:bg-indigo-50"
            onClick={() => onSelect(a.id)}
          >
            <td className="py-2 font-medium text-slate-700">{a.fileName}</td>
            <td>{new Date(a.createdAt).toLocaleString()}</td>
            <td>{a.status}</td>
            <td>{a.totalRequerimientos}</td>
            <td>{a.aprobados}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
```

`web/src/features/history/DetailView.tsx`:

```tsx
import { useEffect, useMemo, useState } from 'react';
import { getAnalysis } from '../../shared/api/client';
import type { RequirementView } from '../../shared/types';
import { RequirementCard } from '../analysis/RequirementCard';

export function DetailView({ id, onBack }: { id: string; onBack: () => void }) {
  const [fileName, setFileName] = useState('');
  const [requirements, setRequirements] = useState<RequirementView[]>([]);
  const [area, setArea] = useState<string>('Todas');
  const [error, setError] = useState<string>();

  useEffect(() => {
    getAnalysis(id)
      .then((detail) => {
        setFileName(detail.fileName);
        setRequirements(
          detail.requerimientos.map((r: any) => ({
            codigo: r.codigo,
            texto: r.texto,
            area: r.area,
            evaluacion: r.evaluacion ?? undefined,
            historias: (r.historias ?? []).map((h: any) => ({ ...h, caso: h.caso ?? undefined })),
          })),
        );
      })
      .catch((e) => setError(e.message));
  }, [id]);

  const areas = useMemo(() => ['Todas', ...new Set(requirements.map((r) => r.area))], [requirements]);
  const visible = area === 'Todas' ? requirements : requirements.filter((r) => r.area === area);

  if (error) return <p className="rounded bg-rose-50 p-3 text-sm text-rose-700">{error}</p>;

  return (
    <div className="space-y-4">
      <button className="text-sm text-indigo-600 hover:underline" onClick={onBack}>
        ← Volver al historial
      </button>
      <h2 className="text-lg font-semibold text-slate-800">{fileName}</h2>
      <div className="flex flex-wrap gap-2">
        {areas.map((a) => (
          <button
            key={a}
            onClick={() => setArea(a)}
            className={`rounded-full px-3 py-1 text-xs font-medium ${
              area === a ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            {a}
          </button>
        ))}
      </div>
      <div className="space-y-3">
        {visible.map((r) => (
          <RequirementCard key={r.codigo} requirement={r} />
        ))}
      </div>
    </div>
  );
}
```

`web/src/App.tsx` (reemplazar):

```tsx
import { useState } from 'react';
import { AnalyzeView } from './features/analysis/AnalyzeView';
import { HistoryView } from './features/history/HistoryView';
import { DetailView } from './features/history/DetailView';

type View = 'analyze' | 'history' | 'detail';

export default function App() {
  const [view, setView] = useState<View>('analyze');
  const [selectedId, setSelectedId] = useState<string>();

  const tab = (target: View, label: string) => (
    <button
      onClick={() => setView(target)}
      className={`rounded-lg px-4 py-2 text-sm font-medium ${
        view === target ? 'bg-indigo-600 text-white' : 'text-slate-600 hover:bg-slate-100'
      }`}
    >
      {label}
    </button>
  );

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-4xl items-center gap-4 px-4 py-3">
          <h1 className="text-lg font-bold text-slate-800">RequirementsCopilot</h1>
          <nav className="ml-auto flex gap-1">
            {tab('analyze', 'Analizar')}
            {tab('history', 'Historial')}
          </nav>
        </div>
      </header>
      <main className="mx-auto max-w-4xl px-4 py-6">
        {view === 'analyze' && <AnalyzeView />}
        {view === 'history' && (
          <HistoryView
            onSelect={(id) => {
              setSelectedId(id);
              setView('detail');
            }}
          />
        )}
        {view === 'detail' && selectedId && <DetailView id={selectedId} onBack={() => setView('history')} />}
      </main>
    </div>
  );
}
```

`web/src/main.tsx` (reemplazar):

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './styles/index.css';
import App from './App';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
```

- [ ] **Step 2: Verificar build y tests**

Run (en `web/`): `npm run build` y `npm test`
Expected: build OK, tests PASS.

- [ ] **Step 3: Smoke manual**

Terminal 1: `dotnet run --project src/RequirementsCopilot.Api` (Fake + InMemory).
Terminal 2 (en `web/`): `npm run dev` → abrir `http://localhost:5173`, subir un `.txt` cualquiera.
Expected: aparecen 3 requerimientos en vivo; REQ-001 y REQ-003 aprobados con historias y casos; REQ-002 no aprobado sin historias. Pestaña Historial lista el análisis; el detalle filtra por área (Pagos, General, Reportes).

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat: vistas Analizar, Historial y Detalle con filtro por área"
```

---

### Task 17: Documentación — ADRs y README

**Files:**
- Create: `docs/adr/template.md` (copiar de `D:\Repositories\Daniela Benitez\JYDE.OpenDataCopilot\docs\adr\template.md`), `docs/adr/0001-stack-dotnet-hexagonal.md`, `docs/adr/0002-foundry-adaptadores-intercambiables.md`, `docs/adr/0003-pipeline-agentes-sin-router.md`, `docs/adr/0004-persistencia-mongodb.md`, `docs/adr/0005-frontend-vite-zustand-tailwind.md`, `README.md`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: decisiones del spec (`docs/superpowers/specs/2026-07-14-requirements-copilot-design.md`).

- [ ] **Step 1: Escribir ADRs**

Formato del template JYDE (Estado/Fecha/Contexto/Decisión/Consecuencias/Alternativas). Contenido por ADR (resumen; redactar 15–30 líneas cada uno):

1. **0001 — Stack .NET hexagonal + DDD**: hereda razones de JYDE ADR-0001 (tipado fuerte, puertos/adaptadores, testabilidad). Capas `Domain ← Application ← Infrastructure ← Api`; Api no referencia Domain (los endpoints consumen DTOs de Application). Alternativa descartada: FastAPI/Python.
2. **0002 — IChatCompletion con adaptadores Fake/Foundry**: puerto único de LLM; `Providers:Chat` selecciona adaptador; Fake permite dev/demo a $0. **Desviación de JYDE**: instrucciones de agente viven en el código (`ChatPrompt.Instructions`) y se usa Chat Completions API estándar, no `agent_reference`/Responses API — proyecto autocontenido sin publicar agentes en el portal de Foundry. Modelo inicial GPT-4.1-mini.
3. **0003 — Pipeline secuencial de agentes sin router**: adapta JYDE ADR-0015. 4 agentes especializados (extractor, evaluador, historias, casos) con prompts pequeños y parseo JSON defensivo. Sin `IAgentRouter`: el flujo es determinista, no conversacional. Guardrail: requerimiento reprobado no genera historias; error de LLM → análisis `Failed`, nunca se inventa contenido. Umbral `Analysis:PassThreshold` configurable (default 3.5).
4. **0004 — Persistencia MongoDB**: documento = agregado `Analysis` completo (mapeado vía `AnalysisDocument`, el dominio no se serializa directo). `Providers:AnalysisRepository` permite `InMemory` para dev/tests. Alternativa descartada: SQL Server (esquema rígido para árboles de análisis).
5. **0005 — Frontend Vite + Zustand + Tailwind**: hereda JYDE ADR-0008/0009/0016. Sin react-router (3 vistas, navegación en estado local — YAGNI). SSE consumido con fetch streaming + parser propio.

- [ ] **Step 2: README.md**

Secciones: qué es (flujo subir → evaluar rúbrica → historias → casos), stack, cómo correr backend (`dotnet run --project src/RequirementsCopilot.Api`, puerto 5100, Swagger en `/swagger`), frontend (`cd web && npm install && npm run dev`), tests (`dotnet test`, `cd web && npm test`), configuración (tabla: `Providers:Chat`, `Providers:AnalysisRepository`, `Analysis:PassThreshold`, `Foundry:*`, `Mongo:*`; secrets por `appsettings.Development.json` o env vars `Foundry__ApiKey`), estructura del repo, enlace a `docs/adr/`.

- [ ] **Step 3: Proteger secrets**

Agregar a `.gitignore`:

```
src/RequirementsCopilot.Api/appsettings.Development.json
```

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "docs: ADRs 0001-0005 y README"
```

---

### Task 18: Validación end-to-end

**Files:** ninguno nuevo (solo verificación; fixes menores si aparecen).

- [ ] **Step 1: Suite completa backend**

Run: `dotnet test`
Expected: PASS, 0 fallos (≈ 34 tests).

- [ ] **Step 2: Suite frontend + build**

Run (en `web/`): `npm test` y `npm run build`
Expected: PASS y build OK.

- [ ] **Step 3: Demo completa con Fake**

Backend + frontend corriendo (Task 16 Step 3). Subir un `.md` real (por ejemplo el spec de este proyecto) y un `.docx`. Verificar: eventos en vivo, historial persistente durante la sesión (InMemory), detalle con chips de área, análisis fallido no rompe la UI (probar subiendo `.xlsx` → toast/mensaje 400).

- [ ] **Step 4 (opcional, requiere credenciales): Foundry real**

En `src/RequirementsCopilot.Api/appsettings.Development.json` poner `Providers:Chat = "Foundry"` + `Foundry:Endpoint/ApiKey/Deployment` reales y repetir la demo con un documento de especificación real. Verificar parseo defensivo (sin excepciones ante prosa/vallas).

- [ ] **Step 5: Commit final si hubo ajustes**

```powershell
git add -A
git commit -m "chore: validación end-to-end"
```

---

## Cobertura spec → tasks

| Spec | Task |
|---|---|
| Estructura hexagonal, Api sin Domain | 1, 13 |
| Dominio (Analysis, rúbrica, historias, casos, área) | 2 |
| Puertos + JsonText | 3 |
| 4 agentes con prompts y parseo defensivo | 4–7 |
| Orquestador + eventos + guardrails | 8 |
| Adaptadores Fake/Foundry por `Providers:Chat` | 9, 10, 13 |
| Extractores PDF/DOCX/TXT/MD, límite 10 MB | 11, 13 |
| Mongo (+ InMemory para dev) | 12, 13 |
| Endpoints SSE + historial + detalle, `{mensaje}` | 13 |
| Frontend: Analizar en vivo, Historial, Detalle con filtro por área | 14–16 |
| ADRs + README | 17 |
| Testing xUnit + Vitest | transversal, 18 |
