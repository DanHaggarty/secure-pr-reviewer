using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecurePrReviewer.Core.Llm;

namespace SecurePrReviewer.App.Llm;

/// <summary>Sends chat completion requests through a LiteLLM OpenAI-compatible gateway.</summary>
public sealed class LiteLlmClient : ILlmClient
{
    private static readonly string Endpoint =
        Environment.GetEnvironmentVariable("LITELLM_ENDPOINT") ?? "http://localhost:4000/v1/chat/completions";
    private static readonly string ModelName =
        Environment.GetEnvironmentVariable("LITELLM_MODEL") ?? "code-model";

    private readonly HttpClient _httpClient;

    /// <summary>Creates a client that sends requests using <paramref name="httpClient"/>.</summary>
    /// <param name="httpClient">HTTP client used for requests; ownership stays with the caller.</param>
    public LiteLlmClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tools = request.Tools is { Count: > 0 }
            ? request.Tools
                .Select(t => new LiteLlmTool(
                    "function",
                    new LiteLlmFunctionDefinition(
                        t.Name,
                        t.Description,
                        JsonSerializer.Deserialize<JsonElement>(t.ParametersSchemaJson))))
                .ToArray()
            : null;

        var payload = new LiteLlmChatRequest(
            ModelName,
            request.Messages.Select(m => new LiteLlmMessage(
                m.Role,
                m.Content,
                m.ToolCalls?.Select(ToLiteLlmToolCall).ToArray(),
                m.ToolCallId)).ToArray(),
            tools);

        using var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(Endpoint, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"LiteLLM request failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}");

        var parsed = JsonSerializer.Deserialize<LiteLlmChatResponse>(body)
            ?? throw new InvalidOperationException("LiteLLM response body was empty.");
        var message = parsed.Choices?.FirstOrDefault()?.Message
            ?? throw new InvalidOperationException("LiteLLM response contained no choices.");

        if (string.IsNullOrEmpty(message.Content) && (message.ToolCalls is null || message.ToolCalls.Count == 0))
            throw new InvalidOperationException("LiteLLM response contained neither content nor tool calls.");

        var toolCalls = message.ToolCalls?
            .Select(tc => new ToolCall(tc.Id, tc.Function.Name, tc.Function.Arguments))
            .ToArray();

        return new ChatCompletionResponse(message.Content, toolCalls);
    }

    private static LiteLlmToolCall ToLiteLlmToolCall(ToolCall toolCall) =>
        new(toolCall.Id, "function", new LiteLlmToolCallFunction(toolCall.Name, toolCall.ArgumentsJson));

    private sealed record LiteLlmChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<LiteLlmMessage> Messages,
        [property: JsonPropertyName("tools"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<LiteLlmTool>? Tools);

    private sealed record LiteLlmTool(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] LiteLlmFunctionDefinition Function);

    private sealed record LiteLlmFunctionDefinition(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("parameters")] JsonElement Parameters);

    private sealed record LiteLlmMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<LiteLlmToolCall>? ToolCalls = null,
        [property: JsonPropertyName("tool_call_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? ToolCallId = null);

    private sealed record LiteLlmToolCall(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] LiteLlmToolCallFunction Function);

    private sealed record LiteLlmToolCallFunction(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("arguments")] string Arguments);

    private sealed record LiteLlmChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<LiteLlmChoice>? Choices);

    private sealed record LiteLlmChoice(
        [property: JsonPropertyName("message")] LiteLlmMessage? Message);
}
