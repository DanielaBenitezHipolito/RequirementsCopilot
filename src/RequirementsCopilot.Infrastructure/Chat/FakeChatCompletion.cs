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
