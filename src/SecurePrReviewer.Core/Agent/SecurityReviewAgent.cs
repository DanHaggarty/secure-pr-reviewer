using System.Text.Json;
using SecurePrReviewer.Core.Llm;
using SecurePrReviewer.Core.Review;

namespace SecurePrReviewer.Core.Agent;

/// <summary>Reviews a pull request diff for security issues using a bounded reason/act loop.</summary>
public sealed class SecurityReviewAgent
{
    private const int MaxIterations = 5;

    private const string SystemPrompt =
        "You are a security reviewer. Inspect the provided diff for vulnerabilities. " +
        "Use read_file or search_repository if you need more context. " +
        "When finished, call submit_findings with your review.";

    private static readonly ToolDefinition ReadFileToolDefinition = new(
        "read_file",
        "Reads a file's contents from the repository.",
        """{"type":"object","properties":{"path":{"type":"string","description":"Path to the file, relative to the repository root."}},"required":["path"]}""");

    private static readonly ToolDefinition SearchRepositoryToolDefinition = new(
        "search_repository",
        "Searches text files in the repository for a literal string.",
        """{"type":"object","properties":{"query":{"type":"string","description":"Literal text to search for."}},"required":["query"]}""");

    private static readonly ToolDefinition SubmitFindingsToolDefinition = new(
        "submit_findings",
        "Submits the completed security review.",
        """
        {
          "type": "object",
          "properties": {
            "findings": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "severity": { "type": "string", "enum": ["HIGH", "MEDIUM", "LOW"] },
                  "title": { "type": "string" },
                  "description": { "type": "string" },
                  "location": { "type": "string" },
                  "recommendation": { "type": "string" }
                },
                "required": ["severity", "title", "description", "location", "recommendation"]
              }
            }
          },
          "required": ["findings"]
        }
        """);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ILlmClient _llmClient;
    private readonly ToolPolicy _toolPolicy;

    /// <summary>Creates an agent that reviews diffs using the given model client and tool policy.</summary>
    /// <param name="llmClient">Client used to reason about the diff.</param>
    /// <param name="toolPolicy">Policy that authorizes and executes model-requested tool calls.</param>
    public SecurityReviewAgent(ILlmClient llmClient, ToolPolicy toolPolicy)
    {
        _llmClient = llmClient;
        _toolPolicy = toolPolicy;
    }

    /// <summary>Reviews <paramref name="request"/>'s diff, returning the model's structured findings.</summary>
    /// <param name="request">The repository path and diff to review.</param>
    /// <param name="cancellationToken">Token used to cancel the review.</param>
    /// <returns>The completed security review.</returns>
    /// <exception cref="InvalidOperationException">The review did not complete within the iteration limit.</exception>
    public async Task<SecurityReview> ReviewAsync(
        ReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new("system", SystemPrompt),
            new("user", $"Review the following diff for security issues:\n\n{request.Diff}")
        };
        var tools = new[] { ReadFileToolDefinition, SearchRepositoryToolDefinition, SubmitFindingsToolDefinition };

        for (var i = 0; i < MaxIterations; i++)
        {
            var response = await _llmClient.CompleteAsync(
                new ChatCompletionRequest(messages, tools), cancellationToken);

            if (response.ToolCalls is { Count: > 0 })
            {
                messages.Add(new ChatMessage("assistant", response.Content, response.ToolCalls));

                // Other tool calls in this batch are intentionally ignored once submit_findings is present.
                var submission = response.ToolCalls.FirstOrDefault(tc => tc.Name == "submit_findings");
                if (submission is not null)
                    return ParseFindings(submission.ArgumentsJson);

                foreach (var toolCall in response.ToolCalls)
                {
                    var observation = await _toolPolicy.ExecuteAsync(toolCall, cancellationToken);
                    messages.Add(new ChatMessage("tool", observation, ToolCallId: toolCall.Id));
                }
            }
            else
            {
                messages.Add(new ChatMessage("assistant", response.Content));
                messages.Add(new ChatMessage("user", "Call submit_findings with your review, or request another tool."));
            }
        }

        throw new InvalidOperationException($"Review did not complete within {MaxIterations} iterations.");
    }

    private static SecurityReview ParseFindings(string argumentsJson) =>
        JsonSerializer.Deserialize<SecurityReview>(argumentsJson, JsonOptions)
            ?? throw new InvalidOperationException("submit_findings arguments could not be parsed.");
}
