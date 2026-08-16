using SecurePrReviewer.Core.Llm;

namespace SecurePrReviewer.Tests
{
    internal sealed class FakeLlmClient : ILlmClient
    {
        private readonly Queue<ChatCompletionResponse> _responses;

        public FakeLlmClient(params ChatCompletionResponse[] responses)
        {
            _responses = new Queue<ChatCompletionResponse>(responses);
        }

        public Task<ChatCompletionResponse> CompleteAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (_responses.Count == 0)
                throw new InvalidOperationException("No more scripted responses.");

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
