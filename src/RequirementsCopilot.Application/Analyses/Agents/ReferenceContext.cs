using System.Text;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Application.Analyses.Agents;

/// <summary>
/// Bloque de contexto con los documentos de referencia del requerimiento, compartido por todos los
/// agentes que trabajan sobre él (evaluador, clarificador, redactores de caso de uso e historias).
/// </summary>
public static class ReferenceContext
{
    /// <summary>Tope de caracteres del bloque (control de costos; los primeros documentos completos).</summary>
    private const int MaxChars = 30_000;

    /// <summary>Bloque formateado, o vacío si el requerimiento no tiene referencias.</summary>
    public static string For(Requirement requirement)
    {
        if (requirement.References.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        builder.Append("\n\nDOCUMENTOS DE REFERENCIA DEL REQUERIMIENTO (solo lectura)\n")
            .Append("Son material de referencia del entregable esperado (plantillas, PDFs modelo, correos tipo): ")
            .Append("úsalos para entender y describir QUÉ debe producir el requerimiento, menciónalos por nombre ")
            .Append("donde aplique, y NO los trates como requerimientos nuevos ni como funcionalidad existente.\n");
        foreach (RequirementReference reference in requirement.References)
        {
            string entry = $"\n--- Referencia «{reference.Name}» ---\n{reference.Content}\n";
            if (builder.Length + entry.Length > MaxChars)
            {
                break;
            }
            builder.Append(entry);
        }
        builder.Append("\n=== FIN DE DOCUMENTOS DE REFERENCIA ===\n");
        return builder.ToString();
    }
}
