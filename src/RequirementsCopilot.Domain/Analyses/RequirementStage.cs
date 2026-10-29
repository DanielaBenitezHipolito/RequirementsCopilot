namespace RequirementsCopilot.Domain.Analyses;

/// <summary>
/// Etapa de gestión del requerimiento en el ciclo de vida del proyecto: de borrador a implementado.
/// La aprobación de la evaluación avanza Draft → Approved; las demás transiciones son manuales.
/// </summary>
public enum RequirementStage
{
    /// <summary>Recién extraído o aún con la evaluación sin aprobar.</summary>
    Draft,

    /// <summary>Evaluación aprobada (automático) o aprobado manualmente.</summary>
    Approved,

    /// <summary>En construcción por el equipo de desarrollo.</summary>
    InDevelopment,

    /// <summary>Entregado en producción.</summary>
    Implemented,
}
