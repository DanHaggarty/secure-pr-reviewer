namespace SecurePrReviewer.Core.Llm;

/// <summary>A single message in a chat completion exchange.</summary>
/// <param name="Role">The message role (e.g. "system", "user", "assistant", "tool").</param>
/// <param name="Content">The message text, or null for an assistant turn that only called tools.</param>
/// <param name="ToolCalls">Tools this assistant turn requested, or null if it did not call any.</param>
/// <param name="ToolCallId">For a "tool" role message, the id of the call this message answers.</param>
public sealed record ChatMessage(
    string Role,
    string? Content,
    IReadOnlyList<ToolCall>? ToolCalls = null,
    string? ToolCallId = null);

/// <summary>A tool the model may choose to call, described by name and a JSON Schema for its parameters.</summary>
/// <param name="Name">The tool's identifier, as the model will refer to it.</param>
/// <param name="Description">A human/model-readable description of what the tool does.</param>
/// <param name="ParametersSchemaJson">The tool's parameters as a raw JSON Schema string, passed through as-is.</param>
public sealed record ToolDefinition(string Name, string Description, string ParametersSchemaJson);

/// <summary>A request from the model to invoke a specific tool with specific arguments.</summary>
/// <param name="Id">Identifier for this call, echoed back when returning the tool's result.</param>
/// <param name="Name">The name of the tool the model wants to invoke.</param>
/// <param name="ArgumentsJson">The tool's arguments as a raw JSON string.</param>
public sealed record ToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>A request to a chat completion model, provider-agnostic.</summary>
/// <param name="Messages">The conversation history to send, in order.</param>
/// <param name="Tools">Tools the model may choose to call; leave null or empty to offer none.</param>
public sealed record ChatCompletionRequest(
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<ToolDefinition>? Tools = null);

/// <summary>The model's reply to a chat completion request.</summary>
/// <param name="Content">The assistant's response text, or null if the model chose to call a tool instead.</param>
/// <param name="ToolCalls">Tools the model wants to invoke, or null if it answered directly.</param>
public sealed record ChatCompletionResponse(
    string? Content,
    IReadOnlyList<ToolCall>? ToolCalls = null);
