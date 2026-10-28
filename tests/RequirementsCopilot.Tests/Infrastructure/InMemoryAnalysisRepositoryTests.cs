using RequirementsCopilot.Domain.Analyses;
using RequirementsCopilot.Infrastructure.Persistence;

namespace RequirementsCopilot.Tests.Infrastructure;

public class InMemoryAnalysisRepositoryTests
{
    [Fact]
    public async Task GetByProjectAsync_FiltraPorProyectoIgnorandoMayusculas()
    {
        var repository = new InMemoryAnalysisRepository();
        await repository.SaveAsync(Analysis.Create("a.txt", "Hotelería"));
        await repository.SaveAsync(Analysis.Create("b.txt", "hotelería"));
        await repository.SaveAsync(Analysis.Create("c.txt", "Otro"));
        await repository.SaveAsync(Analysis.Create("d.txt", null));

        var result = await repository.GetByProjectAsync("HOTELERÍA");

        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal("hotelería", a.ProjectName!.ToLowerInvariant()));
    }

    [Fact]
    public async Task GetByProjectAsync_OrdenaPorFechaAscendente()
    {
        var repository = new InMemoryAnalysisRepository();
        var first = Analysis.Create("a.txt", "P");
        var second = Analysis.Create("b.txt", "P");
        await repository.SaveAsync(second);
        await repository.SaveAsync(first);

        var result = await repository.GetByProjectAsync("P");

        Assert.Equal(2, result.Count);
        Assert.True(result[0].CreatedAt <= result[1].CreatedAt);
    }
}
