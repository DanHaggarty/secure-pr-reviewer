namespace SecurePrReviewer.Core.Llm;

/// <summary>Sends chat completion requests to a language model.</summary>
public interface ILlmClient
{
    /// <summary>Sends <paramref name="request"/> and returns the model's reply.</summary>
    /// <param name="request">The messages to send.</param>
    /// <param name="cancellationToken">Token used to cancel the call.</param>
    Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
