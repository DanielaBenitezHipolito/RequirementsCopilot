using RequirementsCopilot.Application.Analyses;
using RequirementsCopilot.Application.Projects;
using RequirementsCopilot.Domain.Analyses;
using RequirementsCopilot.Infrastructure.Persistence;

namespace RequirementsCopilot.Tests.Application;

public class ProjectContextLoaderTests
{
    private static Analysis AnalysisWithUseCase(string project, string nombre, string objetivo, params string[] actores)
    {
        var analysis = Analysis.Create("doc.txt", project);
        var requirement = Requirement.Create("REQ-001", "texto del requerimiento", "General");
        requirement.SetUseCase(UseCase.Create(nombre, objetivo, null,
            actores.Select(a => new Actor(a, string.Empty)).ToArray(),
            null, null, null, null, null, null, null, null));
        analysis.AddRequirement(requirement);
        return analysis;
    }

    private static ProjectContextLoader Loader(InMemoryProjectRepository projects,
        InMemoryAnalysisRepository analyses, AnalysisOptions? options = null)
        => new(projects, analyses, options ?? new AnalysisOptions());

    [Fact]
    public async Task LoadAsync_SinProyecto_DevuelveVacio()
    {
        var loader = Loader(new InMemoryProjectRepository(), new InMemoryAnalysisRepository());
        Assert.Equal(string.Empty, await loader.LoadAsync(null));
        Assert.Equal(string.Empty, await loader.LoadAsync("Inexistente"));
    }

    [Fact]
    public async Task LoadAsync_ProyectoConMd_MantieneElBloqueDeContexto()
    {
        var projects = new InMemoryProjectRepository();
        await projects.SaveAsync(Project.Create("Hotelería", "Ya existe módulo de pagos."));
        var loader = Loader(projects, new InMemoryAnalysisRepository());

        string context = await loader.LoadAsync("Hotelería");

        Assert.Contains("CONTEXTO DEL PROYECTO EXISTENTE «Hotelería»", context);
        Assert.Contains("Ya existe módulo de pagos.", context);
        Assert.DoesNotContain("CASOS DE USO YA DEFINIDOS", context);
    }

    [Fact]
    public async Task LoadAsync_ProyectoConCasosDeUso_AgregaElApendice()
    {
        var projects = new InMemoryProjectRepository();
        await projects.SaveAsync(Project.Create("Hotelería", "Descripción."));
        var analyses = new InMemoryAnalysisRepository();
        await analyses.SaveAsync(AnalysisWithUseCase("Hotelería", "Registrar pago", "Registrar el pago de una reserva", "Recepcionista"));
        var loader = Loader(projects, analyses);

        string context = await loader.LoadAsync("Hotelería");

        Assert.Contains("CASOS DE USO YA DEFINIDOS DEL PROYECTO «Hotelería»", context);
        Assert.Contains("«Registrar pago»", context);
        Assert.Contains("Registrar el pago de una reserva", context);
        Assert.Contains("Recepcionista", context);
        Assert.Contains("NO los dupliques", context);
    }

    [Fact]
    public async Task LoadAsync_ProyectoSinMdPeroConCasosDeUso_DevuelveSoloElApendice()
    {
        // Proyecto nuevo: se crea sin contenido y el contexto se construye con lo generado.
        var projects = new InMemoryProjectRepository();
        await projects.SaveAsync(Project.Create("Nuevo", string.Empty));
        var analyses = new InMemoryAnalysisRepository();
        await analyses.SaveAsync(AnalysisWithUseCase("Nuevo", "Alta de cliente", "Registrar un cliente", "Analista"));
        var loader = Loader(projects, analyses);

        string context = await loader.LoadAsync("Nuevo");

        Assert.DoesNotContain("El sistema YA existe", context);
        Assert.Contains("CASOS DE USO YA DEFINIDOS DEL PROYECTO «Nuevo»", context);
        Assert.Contains("«Alta de cliente»", context);
    }

    [Fact]
    public async Task LoadAsync_ProyectoConEjemplos_IncluyeElBloqueDeSoloLectura()
    {
        var projects = new InMemoryProjectRepository();
        Project project = Project.Create("P", "Descripción.")
            .AddExample("respuesta-final-cliente.pdf", "Estimado cliente: adjunto la propuesta…");
        await projects.SaveAsync(project);
        var loader = Loader(projects, new InMemoryAnalysisRepository());

        string context = await loader.LoadAsync("P");

        Assert.Contains("DOCUMENTOS DE EJEMPLO DEL PROYECTO «P»", context);
        Assert.Contains("respuesta-final-cliente.pdf", context);
        Assert.Contains("Estimado cliente: adjunto la propuesta…", context);
        Assert.Contains("solo lectura", context);
        Assert.Contains("NO extraigas requerimientos", context);
    }

    [Fact]
    public async Task LoadAsync_SinEjemplos_NoIncluyeElBloque()
    {
        var projects = new InMemoryProjectRepository();
        await projects.SaveAsync(Project.Create("P", "Descripción."));
        var loader = Loader(projects, new InMemoryAnalysisRepository());
        Assert.DoesNotContain("DOCUMENTOS DE EJEMPLO", await loader.LoadAsync("P"));
    }

    [Fact]
    public async Task LoadAsync_CasosDeUsoDeOtroProyecto_NoSeIncluyen()
    {
        var projects = new InMemoryProjectRepository();
        await projects.SaveAsync(Project.Create("Hotelería", "Descripción."));
        var analyses = new InMemoryAnalysisRepository();
        await analyses.SaveAsync(AnalysisWithUseCase("Otro", "Caso ajeno", "Objetivo ajeno", "Actor"));
        var loader = Loader(projects, analyses);

        string context = await loader.LoadAsync("Hotelería");

        Assert.DoesNotContain("Caso ajeno", context);
        Assert.DoesNotContain("CASOS DE USO YA DEFINIDOS", context);
    }

    [Fact]
    public async Task LoadAsync_ApendiceLargo_SeTruncaAlTope()
    {
        var projects = new InMemoryProjectRepository();
        await projects.SaveAsync(Project.Create("P", "x"));
        var analyses = new InMemoryAnalysisRepository();
        for (int i = 0; i < 50; i++)
        {
            await analyses.SaveAsync(AnalysisWithUseCase("P", $"Caso {i:000}", new string('o', 100), "Actor"));
        }
        var options = new AnalysisOptions { MaxProjectContextChars = 1000 };
        var loader = Loader(projects, analyses, options);

        string context = await loader.LoadAsync("P");

        // El apéndice no excede el tope (más el cierre del bloque) y conserva los primeros casos.
        Assert.Contains("«Caso 000»", context);
        Assert.DoesNotContain("«Caso 049»", context);
        Assert.True(context.Length < 2500, $"Contexto demasiado largo: {context.Length}");
    }

    [Fact]
    public async Task LoadAsync_RequerimientoSinCasoDeUso_NoAportaEntrada()
    {
        var projects = new InMemoryProjectRepository();
        await projects.SaveAsync(Project.Create("P", "x"));
        var analyses = new InMemoryAnalysisRepository();
        var analysis = Analysis.Create("doc.txt", "P");
        analysis.AddRequirement(Requirement.Create("REQ-001", "sin caso de uso", "General"));
        await analyses.SaveAsync(analysis);
        var loader = Loader(projects, analyses);

        Assert.DoesNotContain("CASOS DE USO YA DEFINIDOS", await loader.LoadAsync("P"));
    }
}
