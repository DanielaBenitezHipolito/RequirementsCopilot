using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class ClarifierAgentTests
{
    private static Requirement Req() => Requirement.Create("REQ-001", "El sistema debe registrar pagos", "Pagos");

    [Fact]
    public async Task AskAsync_FormatoConOpciones_DevuelveTextoYOpciones()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"preguntas\":[{\"pregunta\":\"¿Qué medios de pago acepta?\"," +
                "\"opciones\":[\"Solo tarjeta\",\"Tarjeta y efectivo\",\"Todos los medios\"]}]}",
        };
        var agent = new ClarifierAgent(chat);

        var questions = await agent.AskAsync(Req());

        ClarifierQuestion question = Assert.Single(questions);
        Assert.Equal("¿Qué medios de pago acepta?", question.Text);
        Assert.Equal(new[] { "Solo tarjeta", "Tarjeta y efectivo", "Todos los medios" }, question.Options);
    }

    [Fact]
    public async Task AskAsync_FormatoLegadoDeTextos_DevuelvePreguntasSinOpciones()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"preguntas\":[\"¿Qué significa rápido en segundos?\"]}",
        };
        var agent = new ClarifierAgent(chat);

        var questions = await agent.AskAsync(Req());

        ClarifierQuestion question = Assert.Single(questions);
        Assert.Equal("¿Qué significa rápido en segundos?", question.Text);
        Assert.Empty(question.Options);
    }

    [Fact]
    public async Task AskAsync_FormatoMixto_SoportaAmbos()
    {
        var chat = new StubChatCompletion
        {
            Reply = _ => "{\"preguntas\":[\"¿Quién lo usa?\"," +
                "{\"pregunta\":\"¿Aplica a reservas web?\",\"opciones\":[\"Sí\",\"No\"]}]}",
        };
        var agent = new ClarifierAgent(chat);

        var questions = await agent.AskAsync(Req());

        Assert.Equal(2, questions.Count);
        Assert.Empty(questions[0].Options);
        Assert.Equal(new[] { "Sí", "No" }, questions[1].Options);
    }
}
