namespace RequirementsCopilot.Application.Usage;

/// <summary>
/// Precio contratado de un modelo en USD por millón de tokens (sección <c>Pricing</c> de la
/// configuración; p. ej. <c>Pricing__gpt-5-mini__InputPer1M</c>). Sin precios configurados el
/// tablero muestra solo tokens: las cifras de dinero nunca se inventan.
/// </summary>
public sealed class ModelPrice
{
    /// <summary>USD por millón de tokens de entrada.</summary>
    public decimal InputPer1M { get; set; }

    /// <summary>USD por millón de tokens de salida.</summary>
    public decimal OutputPer1M { get; set; }
}
