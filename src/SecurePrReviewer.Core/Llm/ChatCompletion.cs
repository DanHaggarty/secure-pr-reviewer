namespace SecurePrReviewer.Core.Llm;

/// <summary>A single message in a chat completion exchange.</summary>
/// <param name="Role">The message role (e.g. "system", "user", "assistant").</param>
/// <param name="Content">The message text.</param>
public sealed record ChatMessage(string Role, string Content);

/// <summary>A request to a chat completion model, provider-agnostic.</summary>
/// <param name="Messages">The conversation history to send, in order.</param>
public sealed record ChatCompletionRequest(IReadOnlyList<ChatMessage> Messages);

/// <summary>The model's reply to a chat completion request.</summary>
/// <param name="Content">The assistant's response text.</param>
public sealed record ChatCompletionResponse(string Content);
