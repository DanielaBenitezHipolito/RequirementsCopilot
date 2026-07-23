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

    private const string ClarifierReply =
        "{\"preguntas\":[" +
        "\"¿Qué tiempo de respuesta máximo, en segundos, se considera aceptable?\"," +
        "\"¿Qué tareas concretas debe poder completar el usuario sin capacitación?\"]}";

    private const string UseCaseReply =
        "{\"nombre\":\"Módulo de Pólizas – Sistema HC Consulting\"," +
        "\"objetivo\":\"Permitir el registro del pago de una reserva con tarjeta, dejando trazabilidad del monto, la fecha y el consecutivo generado.\"," +
        "\"descripcion\":\"El recepcionista registra el pago de una reserva existente; el sistema valida el monto, genera un consecutivo único y marca la reserva como pagada.\"," +
        "\"actores\":[{\"nombre\":\"Recepcionista\",\"descripcion\":\"Usuario que atiende al huésped y registra el pago en el sistema.\"}," +
        "{\"nombre\":\"Sistema de Pagos\",\"descripcion\":\"Componente que valida el monto y genera el consecutivo de pago.\"}]," +
        "\"precondiciones\":[\"La reserva debe existir y estar activa\",\"El recepcionista debe tener sesión iniciada\"]," +
        "\"trigger\":\"El huésped se presenta a pagar su reserva en recepción.\"," +
        "\"flujos\":[{\"titulo\":\"Proceso de creación manual\",\"pasos\":[" +
        "{\"numero\":1,\"accion\":\"El recepcionista abre la reserva del huésped\",\"resultadoEsperado\":\"El sistema muestra el detalle de la reserva\"}," +
        "{\"numero\":2,\"accion\":\"El recepcionista ingresa el monto y los datos de la tarjeta\",\"resultadoEsperado\":\"El sistema valida el monto contra el saldo pendiente\"}," +
        "{\"numero\":3,\"accion\":\"El recepcionista confirma el pago\",\"resultadoEsperado\":\"El sistema genera el consecutivo y marca la reserva como pagada\"}]}]," +
        "\"extensiones\":[\"Si el monto no coincide con el saldo pendiente, el sistema rechaza el pago y muestra un mensaje de error\"," +
        "\"Si la tarjeta es rechazada, el sistema no genera consecutivo y la reserva permanece sin pagar\"]," +
        "\"frecuencia\":\"Única\",\"importancia\":\"Alta\",\"urgencia\":\"Alta\"," +
        "\"comentarios\":[\"El consecutivo debe ser único por empresa\"]}";

    private const string BuilderQuestionReply =
        "{\"listo\":false,\"mensaje\":\"¿Quién usará esta funcionalidad y qué dato debe quedar registrado al final?\",\"requerimiento\":null}";

    private const string BuilderReadyReply =
        "{\"listo\":true,\"mensaje\":\"Con eso es suficiente; este es el requerimiento propuesto.\"," +
        "\"requerimiento\":{\"texto\":\"El sistema debe permitir al recepcionista registrar el pago de una reserva, guardando monto, fecha y consecutivo.\",\"area\":\"Pagos\"}}";

    private const string ExecutiveSummaryReply =
        "{\"resumen\":\"La auditoría evidencia un documento con requerimientos mayormente claros y verificables, " +
        "aunque persisten vacíos en criterios medibles y alcance en algunas áreas. Se recomienda cerrar las " +
        "aclaraciones pendientes antes de avanzar a diseño para reducir el riesgo de retrabajo.\"}";

    public Task<ChatResult> CompleteAsync(ChatPrompt prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        string text = prompt.Agent switch
        {
            RequirementExtractorAgent.AgentName => ExtractorReply,
            RequirementEvaluatorAgent.AgentName => prompt.Input.Contains("Aclaraciones respondidas")
                ? HighRubric
                : IsOddRequirement(prompt.Input) ? HighRubric : LowRubric,
            ClarifierAgent.AgentName => ClarifierReply,
            UseCaseWriterAgent.AgentName => UseCaseReply,
            // ponytail: guion fijo — 1a llamada pregunta, con hilo previo redacta. Suficiente para demo sin credenciales.
            RequirementBuilderAgent.AgentName =>
                string.IsNullOrEmpty(prompt.PreviousResponseId) ? BuilderQuestionReply : BuilderReadyReply,
            ExecutiveSummaryAgent.AgentName => ExecutiveSummaryReply,
            _ => "{}",
        };
        return Task.FromResult(new ChatResult(text, "fake-response-id"));
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
