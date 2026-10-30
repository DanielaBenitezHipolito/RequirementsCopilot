namespace RequirementsCopilot.Application.Usage;

/// <summary>Cálculo del costo estimado del consumo de LLM según los precios contratados.</summary>
public static class UsageCost
{
    /// <summary>
    /// Costo estimado en USD del consumo, o null si no hay ningún precio configurado (sin precios
    /// no se muestran cifras de dinero). Entradas de modelos sin precio aportan 0.
    /// </summary>
    public static decimal? Calculate(IEnumerable<LlmUsage> entries,
        IReadOnlyDictionary<string, ModelPrice> prices)
    {
        if (prices.Count == 0)
        {
            return null;
        }
        decimal total = 0m;
        foreach (LlmUsage entry in entries)
        {
            if (prices.TryGetValue(entry.Model, out ModelPrice? price))
            {
                total += entry.InputTokens / 1_000_000m * price.InputPer1M
                       + entry.OutputTokens / 1_000_000m * price.OutputPer1M;
            }
        }
        return total;
    }
}
