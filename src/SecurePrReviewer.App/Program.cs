using SecurePrReviewer.App.Llm;
using SecurePrReviewer.Core.Llm;

// Temporary smoke test proving the LiteLLM chat completion wiring works end to end.
// Not part of the agent loop yet — see ADR-0001/ADR-0002/ADR-0005.
using var httpClient = new HttpClient();
ILlmClient llmClient = new LiteLlmClient(httpClient);

try
{
    var response = await llmClient.CompleteAsync(
        new ChatCompletionRequest(new[]
        {
            new ChatMessage("user", "Reply with exactly: pong")
        }));

    Console.WriteLine(response.Content);
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"LLM request failed: {ex.Message}");
}
