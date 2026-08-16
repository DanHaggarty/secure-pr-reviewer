using SecurePrReviewer.App.Llm;
using SecurePrReviewer.Core.Llm;

// Temporary smoke test proving the LiteLLM chat completion + tool-calling wiring works end to end.
// Not part of the agent loop yet — see ADR-0001/ADR-0002/ADR-0003/ADR-0005.
using var httpClient = new HttpClient();
ILlmClient llmClient = new LiteLlmClient(httpClient);

var tools = new[]
{
    new ToolDefinition(
        "read_file",
        "Reads a file's contents from the repository.",
        """{"type":"object","properties":{"path":{"type":"string","description":"Path to the file, relative to the repository root."}},"required":["path"]}"""),
    new ToolDefinition(
        "search_repository",
        "Searches text files in the repository for a literal string.",
        """{"type":"object","properties":{"query":{"type":"string","description":"Literal text to search for."}},"required":["query"]}"""),
};

try
{
    var response = await llmClient.CompleteAsync(new ChatCompletionRequest(
        new[] { new ChatMessage("user", "Use the read_file tool to read Program.cs") },
        tools));

    if (response.ToolCalls is { Count: > 0 })
    {
        foreach (var toolCall in response.ToolCalls)
            Console.WriteLine($"Tool call requested: {toolCall.Name}({toolCall.ArgumentsJson})");
    }
    else
    {
        Console.WriteLine(response.Content);
    }
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"LLM request failed: {ex.Message}");
}
