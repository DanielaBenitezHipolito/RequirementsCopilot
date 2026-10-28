using RequirementsCopilot.Infrastructure.Chat;

namespace RequirementsCopilot.Tests.Infrastructure;

public class FilePromptCatalogTests
{
    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "prompts-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Get_AgenteConArchivo_DevuelveElPrompt()
    {
        string dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "agente-x.md"), "Eres un agente de prueba.\n");
        var catalog = new FilePromptCatalog(dir);
        Assert.Equal("Eres un agente de prueba.", catalog.Get("agente-x").Trim());
    }

    [Fact]
    public void Get_AgenteSinArchivo_LanzaConElNombre()
    {
        var catalog = new FilePromptCatalog(TempDir());
        var ex = Assert.Throws<KeyNotFoundException>(() => catalog.Get("agente-y"));
        Assert.Contains("agente-y", ex.Message);
    }

    [Fact]
    public void Ctor_DirectorioInexistente_Lanza()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Assert.Throws<DirectoryNotFoundException>(() => new FilePromptCatalog(missing));
    }
}
