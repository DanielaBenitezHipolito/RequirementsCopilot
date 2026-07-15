namespace RequirementsCopilot.Application.Analyses;

public interface IChatCompletion
{
    Task<ChatResult> CompleteAsync(ChatPrompt prompt, CancellationToken cancellationToken = default);
}
