using RequirementsCopilot.Application.Usage;

namespace RequirementsCopilot.Tests.Application;

public class UsageCostTests
{
    private static LlmUsage Entry(string model, int input, int output)
        => new(DateTime.UtcNow, "agente-x", model, input, output);

    [Fact]
    public void Calculate_SinPreciosConfigurados_DevuelveNull()
    {
        decimal? cost = UsageCost.Calculate(
            new[] { Entry("gpt-5-mini", 1_000_000, 1_000_000) },
            new Dictionary<string, ModelPrice>());
        Assert.Null(cost);
    }

    [Fact]
    public void Calculate_ConPrecio_CalculaEntradaYSalidaPorMillon()
    {
        var prices = new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5-mini"] = new ModelPrice { InputPer1M = 0.25m, OutputPer1M = 2.00m },
        };

        decimal? cost = UsageCost.Calculate(
            new[] { Entry("gpt-5-mini", 2_000_000, 500_000) },
            prices);

        // 2M entrada × 0.25 + 0.5M salida × 2.00 = 0.50 + 1.00
        Assert.Equal(1.50m, cost);
    }

    [Fact]
    public void Calculate_ModeloSinPrecio_Aporta0PeroNoAnulaElTotal()
    {
        var prices = new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5-mini"] = new ModelPrice { InputPer1M = 1.00m, OutputPer1M = 1.00m },
        };

        decimal? cost = UsageCost.Calculate(
            new[] { Entry("gpt-5-mini", 1_000_000, 0), Entry("otro-modelo", 9_000_000, 9_000_000) },
            prices);

        Assert.Equal(1.00m, cost);
    }

    [Fact]
    public void Calculate_IgnoraMayusculasDelModelo()
    {
        var prices = new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
        {
            ["GPT-5-MINI"] = new ModelPrice { InputPer1M = 1.00m, OutputPer1M = 1.00m },
        };
        Assert.Equal(2.00m, UsageCost.Calculate(new[] { Entry("gpt-5-mini", 1_000_000, 1_000_000) }, prices));
    }
}
