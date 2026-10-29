namespace RequirementsCopilot.Application.Usage;

/// <summary>Puerto de persistencia del consumo de LLM.</summary>
public interface ILlmUsageRepository
{
    /// <summary>Registra el consumo de una llamada.</summary>
    Task AddAsync(LlmUsage usage, CancellationToken cancellationToken = default);

    /// <summary>Consumos desde la fecha indicada (UTC), ordenados por fecha ascendente.</summary>
    Task<IReadOnlyList<LlmUsage>> GetSinceAsync(DateTime since, CancellationToken cancellationToken = default);
}
