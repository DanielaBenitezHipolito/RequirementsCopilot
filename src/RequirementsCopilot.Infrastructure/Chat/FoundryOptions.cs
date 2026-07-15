namespace RequirementsCopilot.Infrastructure.Chat;

public sealed record FoundryOptions
{
    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string Deployment { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = "2024-10-21";
}
