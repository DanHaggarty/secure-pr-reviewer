using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecurePrReviewer.Core.Llm;

namespace SecurePrReviewer.App.Llm;

/// <summary>Sends chat completion requests through a LiteLLM OpenAI-compatible gateway.</summary>
public sealed class LiteLlmClient : ILlmClient
{
    private const string Endpoint = "http://localhost:4000/v1/chat/completions";
    private const string ModelName = "code-model";

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
        var payload = new LiteLlmChatRequest(
            ModelName,
            request.Messages.Select(m => new LiteLlmMessage(m.Role, m.Content)).ToArray());

        using var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(Endpoint, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"LiteLLM request failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}");

        var parsed = JsonSerializer.Deserialize<LiteLlmChatResponse>(body)
            ?? throw new InvalidOperationException("LiteLLM response body was empty.");
        var messageContent = parsed.Choices?.FirstOrDefault()?.Message?.Content
            ?? throw new InvalidOperationException("LiteLLM response contained no choices.");

        return new ChatCompletionResponse(messageContent);
    }

    private sealed record LiteLlmChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<LiteLlmMessage> Messages);

    private sealed record LiteLlmMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record LiteLlmChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<LiteLlmChoice>? Choices);

    private sealed record LiteLlmChoice(
        [property: JsonPropertyName("message")] LiteLlmMessage? Message);
}
