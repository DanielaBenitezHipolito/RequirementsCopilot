namespace RequirementsCopilot.Application.Conversations;

/// <summary>Un turno de la conversación. Role: "user" | "agent".</summary>
public sealed record ConversationMessage(string Role, string Text);
