using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Domain;

public class RequirementReferencesTests
{
    private static Requirement Req() => Requirement.Create("REQ-001", "Formulario con envío de correo", "General");

    [Fact]
    public void AddReference_AgregaDocumento()
    {
        var requirement = Req();
        requirement.AddReference("pdf-diligenciado.pdf", "Campos: nombre, póliza, firma…");
        RequirementReference reference = Assert.Single(requirement.References);
        Assert.Equal("pdf-diligenciado.pdf", reference.Name);
        Assert.Equal("Campos: nombre, póliza, firma…", reference.Content);
    }

    [Fact]
    public void AddReference_MismoNombre_Reemplaza()
    {
        var requirement = Req();
        requirement.AddReference("ejemplo.pdf", "v1");
        requirement.AddReference("EJEMPLO.PDF", "v2");
        RequirementReference reference = Assert.Single(requirement.References);
        Assert.Equal("v2", reference.Content);
    }

    [Fact]
    public void RemoveReference_QuitaPorNombre()
    {
        var requirement = Req();
        requirement.AddReference("ejemplo.pdf", "contenido");
        Assert.True(requirement.RemoveReference("Ejemplo.pdf"));
        Assert.Empty(requirement.References);
        Assert.False(requirement.RemoveReference("ejemplo.pdf"));
    }
}
