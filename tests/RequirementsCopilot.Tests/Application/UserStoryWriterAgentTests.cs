using RequirementsCopilot.Application.Analyses.Agents;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Tests.Application;

public class UserStoryWriterAgentTests
{
    private const string Reply =
        "{\"historias\":[" +
        "{\"titulo\":\"Notificar al CFO\",\"como\":\"Director\",\"quiero\":\"notificar al CFO al autorizar\"," +
        "\"para\":\"iniciar la autorización financiera\",\"criteriosAceptacion\":[\"Se envía correo\",\" \"]," +
        "\"puntos\":3,\"dependencias\":[]}," +
        "{\"titulo\":\"\",\"puntos\":3}," +
        "{\"titulo\":\"Puntos inválidos\",\"puntos\":4}]}";

    private static Requirement Ready()
    {
        Requirement requirement = Requirement.Create("REQ-001", "Notificar al CFO al autorizar", "Pagos");
        requirement.Evaluate(Evaluation.Create(
            new[] { CriterionScore.Create("Claridad", 5, "ok") }, 3.5));
        return requirement;
    }

    [Fact]
    public async Task WriteAsync_FiltraInvalidasYLimpiaCriterios()
    {
        var chat = new StubChatCompletion { Reply = _ => Reply };
        var stories = await new UserStoryWriterAgent(chat).WriteAsync(Ready());

        var story = Assert.Single(stories);
        Assert.Equal("Notificar al CFO", story.Titulo);
        Assert.Equal(3, story.Puntos);
        Assert.Single(story.CriteriosAceptacion);
    }

    [Fact]
    public async Task WriteAsync_IncluyeContextoAclaracionesYCasoDeUso()
    {
        Requirement requirement = Ready();
        requirement.AddClarification(Clarification.Create("¿Umbral?"));
        requirement.Clarifications[0].Respond("El configurado por país");
        requirement.SetUseCase(UseCase.Create("Notificar CFO", "Objetivo X", "Descripción Y",
            null, null, null, null, null, null, null, null, null));

        var chat = new StubChatCompletion { Reply = _ => Reply };
        await new UserStoryWriterAgent(chat).WriteAsync(requirement, "CONTEXTO DEL PROYECTO EXISTENTE «HR»\n...\n");

        string input = chat.Prompts[0].Input;
        Assert.Equal(UserStoryWriterAgent.AgentName, chat.Prompts[0].Agent);
        Assert.StartsWith("CONTEXTO DEL PROYECTO EXISTENTE «HR»", input);
        Assert.Contains("El configurado por país", input);
        Assert.Contains("Objetivo X", input);
    }

    [Fact]
    public async Task WriteAsync_SinHistoriasValidas_Lanza()
    {
        var chat = new StubChatCompletion { Reply = _ => "{\"historias\":[]}" };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new UserStoryWriterAgent(chat).WriteAsync(Ready()));
    }
}
