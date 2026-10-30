using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class ReferenceContextTests
{
    [Fact]
    public void For_SinReferencias_DevuelveVacio()
    {
        var requirement = Requirement.Create("REQ-001", "texto", "General");
        Assert.Equal(string.Empty, ReferenceContext.For(requirement));
    }

    [Fact]
    public void For_ConReferencias_IncluyeNombreContenidoEInstruccion()
    {
        var requirement = Requirement.Create("REQ-001", "texto", "General");
        requirement.AddReference("respuesta-cliente.pdf", "Estimado cliente: adjunto el formulario…");

        string block = ReferenceContext.For(requirement);

        Assert.Contains("DOCUMENTOS DE REFERENCIA DEL REQUERIMIENTO", block);
        Assert.Contains("respuesta-cliente.pdf", block);
        Assert.Contains("Estimado cliente: adjunto el formulario…", block);
        Assert.Contains("referencia del entregable esperado", block);
    }
}
