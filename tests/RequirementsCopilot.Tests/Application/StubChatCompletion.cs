using RequirementsCopilot.Application.Analyses;

namespace RequirementsCopilot.Tests.Application;

public sealed class StubChatCompletion : IChatCompletion
{
    public Func<ChatPrompt, string> Reply { get; set; } = _ => "{}";
    public List<ChatPrompt> Prompts { get; } = new();

    public Task<ChatResult> CompleteAsync(ChatPrompt prompt, CancellationToken cancellationToken = default)
    {
        Prompts.Add(prompt);
        return Task.FromResult(new ChatResult(Reply(prompt)));
    }
}
