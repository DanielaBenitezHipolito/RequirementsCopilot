using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class UseCaseWriterAgentTests
{
    private const string FullContractReply =
        "{\"nombre\":\"Módulo de Pólizas – Sistema HC Consulting\"," +
        "\"objetivo\":\"Registrar el pago de una reserva.\"," +
        "\"descripcion\":\"El cajero registra el pago de una reserva existente.\"," +
        "\"actores\":[{\"nombre\":\"Cajero\",\"descripcion\":\"Registra el pago.\"}]," +
        "\"precondiciones\":[\"La reserva debe existir\"]," +
        "\"trigger\":\"El huésped paga en caja.\"," +
        "\"flujos\":[{\"titulo\":\"Proceso de creación manual\",\"pasos\":[" +
        "{\"numero\":1,\"accion\":\"Abrir la reserva\",\"resultadoEsperado\":\"Se muestra el detalle\"}," +
        "{\"numero\":2,\"accion\":\"Registrar el monto\",\"resultadoEsperado\":\"Queda registrado el pago\"}]}]," +
        "\"extensiones\":[\"Si el monto no coincide, se rechaza el pago\"]," +
        "\"frecuencia\":\"Única\",\"importancia\":\"Alta\",\"urgencia\":\"Alta\"," +
        "\"comentarios\":[\"Ninguno\"]}";

    [Fact]
    public async Task WriteAsync_ContratoCompleto_DevuelveUseCaseConTodasLasSecciones()
    {
        var chat = new StubChatCompletion { Reply = _ => FullContractReply };
        var agent = new UseCaseWriterAgent(chat);
        var requirement = Requirement.Create("REQ-001", "El sistema debe registrar pagos", "Pagos");

        UseCase useCase = await agent.WriteAsync(requirement);

        Assert.Equal("Módulo de Pólizas – Sistema HC Consulting", useCase.Nombre);
        Assert.Equal("Registrar el pago de una reserva.", useCase.Objetivo);
        Assert.Equal("El cajero registra el pago de una reserva existente.", useCase.Descripcion);
        var actor = Assert.Single(useCase.Actores);
        Assert.Equal("Cajero", actor.Nombre);
        Assert.Equal("Registra el pago.", actor.Descripcion);
        Assert.Single(useCase.Precondiciones);
        Assert.Equal("El huésped paga en caja.", useCase.Trigger);
        var flujo = Assert.Single(useCase.Flujos);
        Assert.Equal("Proceso de creación manual", flujo.Titulo);
        Assert.Equal(2, flujo.Pasos.Count);
        Assert.Equal(1, flujo.Pasos[0].Numero);
        Assert.Equal("Abrir la reserva", flujo.Pasos[0].Accion);
        Assert.Equal("Se muestra el detalle", flujo.Pasos[0].ResultadoEsperado);
        Assert.Single(useCase.Extensiones);
        Assert.Equal("Única", useCase.Frecuencia);
        Assert.Equal("Alta", useCase.Importancia);
        Assert.Equal("Alta", useCase.Urgencia);
        Assert.Single(useCase.Comentarios);
        Assert.Equal("use-case-writer-agent", chat.Prompts[0].Agent);
    }

    [Fact]
    public async Task WriteAsync_SinJson_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "..." };
        var agent = new UseCaseWriterAgent(chat);
        var requirement = Requirement.Create("REQ-001", "texto", "General");
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.WriteAsync(requirement));
    }

    [Fact]
    public async Task WriteAsync_ConAclaracionesRespondidas_LasIncluyeEnElInput()
    {
        var chat = new StubChatCompletion { Reply = _ => FullContractReply };
        var agent = new UseCaseWriterAgent(chat);
        var requirement = Requirement.Create("REQ-001", "El sistema debe ser rápido", "General");
        requirement.AddClarification(Clarification.Create("¿Qué significa rápido?"));
        requirement.Clarifications[0].Respond("Menos de 2 segundos");

        await agent.WriteAsync(requirement);

        Assert.Contains("Aclaraciones del cliente", chat.Prompts[0].Input);
        Assert.Contains("Menos de 2 segundos", chat.Prompts[0].Input);
    }
}
