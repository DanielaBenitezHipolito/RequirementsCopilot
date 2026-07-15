namespace RequirementsCopilot.Infrastructure.Mongo;

public sealed record MongoOptions
{
    public string ConnectionString { get; init; } = string.Empty;
    public string Database { get; init; } = "requirements_copilot";
}
