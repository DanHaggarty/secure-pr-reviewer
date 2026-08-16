# Agent Loop Design

## Status

Proposed

## Context

The project now has a security boundary for repository access (`RepositoryPathResolver`, `ReadFileTool`, `SearchRepositoryTool`) and an LLM client capable of full tool-calling (`ILlmClient`/`LiteLlmClient`, offering tool definitions and parsing tool-call requests). What's still missing is the piece that ties them together: the agent loop described in ADR-0001 (`reason → request tool → validate → execute → observe → reason`), with the tool-authorization enforcement point named by ADR-0003/0004 and the `ReviewRequest(repoPath, diff)` entry point described by ADR-0005.

This spec covers building that loop and its tool-authorization layer. It deliberately **excludes**:
- The input guard (scanning the diff/repo content before it reaches the model).
- The output guard (secret/sensitive-output redaction).
- The adversarial "killer demo" scenario (a malicious file in the repo attempting prompt injection) and any structured audit-trail output (`blockedActions`, iteration counts, etc. as a first-class result).

These are natural follow-on increments once the loop mechanics exist and are proven correct on their own. The structured audit-trail concept in particular is explicitly flagged here for later — see **Deferred** below — rather than silently dropped.

## Decision

### Architecture

```
ReviewRequest(RepoPath, Diff)
        │
        ▼
SecurityReviewAgent.ReviewAsync(request, ct) → Task<SecurityReview>
        │
        ▼
  seed messages: [system prompt, "review this diff: <diff>"]
        │
        ▼
  loop (max 5 iterations):
    ILlmClient.CompleteAsync(messages, tools=[read_file, search_repository, submit_findings])
      ├─ ToolCalls present, includes submit_findings → parse args, return SecurityReview (done)
      ├─ ToolCalls present, read_file/search_repository → ToolPolicy.ExecuteAsync each,
      │     append assistant turn + tool-result turn(s) to messages, loop again
      └─ no ToolCalls (plain text) → append it, nudge model to call submit_findings, loop again
  iteration limit reached without submit_findings → throw InvalidOperationException
```

`SecurityReviewAgent.ReviewAsync` matches ADR-0006's example signature exactly: `Task<SecurityReview> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default)`.

Both `SecurityReviewAgent` and `ToolPolicy` live in **`SecurePrReviewer.Core`**, not App — they depend only on `ILlmClient` (the interface) and other Core types (`ReadFileTool`, `SearchRepositoryTool`). Nothing here is HTTP- or LiteLLM-specific. Only `Program.cs` (App) needs to construct the concrete `LiteLlmClient` and wire it into a `SecurityReviewAgent`.

The iteration limit is **5**, matching the number floated in the project's original design brainstorm for this exact loop shape.

### New Core types (`src/SecurePrReviewer.Core/Review/SecurityReview.cs`)

Grouping related, mutually-referential types in one file, per the established convention (`SearchRepositoryTool.cs`, `ChatCompletion.cs`):

```csharp
public sealed record ReviewRequest(string RepoPath, string Diff);

public sealed record SecurityFinding(
    string Severity,      // e.g. "HIGH", "MEDIUM", "LOW" — matches the README's example output
    string Title,
    string Description,
    string Location,
    string Recommendation);

public sealed record SecurityReview(IReadOnlyList<SecurityFinding> Findings);
```

### Changed — `src/SecurePrReviewer.Core/Llm/ChatCompletion.cs`

`ChatMessage` gains two nullable fields to round-trip a multi-turn tool-calling conversation — an assistant turn that requested tools, and the tool-result turn answering it:

```csharp
public sealed record ChatMessage(
    string Role,
    string? Content,
    IReadOnlyList<ToolCall>? ToolCalls = null,   // set on assistant turns that called tools
    string? ToolCallId = null);                   // set on "tool" role turns, echoes ToolCall.Id
```

This mirrors the real OpenAI wire message shape (itself optional-by-role for these fields) rather than introducing a message type hierarchy — consistent with the existing "nullable optional fields on one type" pattern already used for `ChatCompletionResponse`.

**Knock-on effect:** `LiteLlmClient`'s private `LiteLlmMessage` DTO currently only builds request messages from `(Role, Content)`. It needs extending to serialize `tool_calls`/`tool_call_id` when round-tripping — part of this same increment, not a new one.

### `ToolPolicy` (`src/SecurePrReviewer.Core/Agent/ToolPolicy.cs`)

Given a `ToolCall` for `read_file` or `search_repository`, authorizes and executes it, always returning a plain observation string — **never throws**. This is the concrete enforcement point for ADR-0003/0004: the model can request anything, but only the app decides what actually runs.

```
ToolPolicy.ExecuteAsync(ToolCall, ct) → Task<string>
  "read_file"          → parse {path} from ArgumentsJson → ReadFileTool.ExecuteAsync
  "search_repository"  → parse {query} from ArgumentsJson → SearchRepositoryTool.ExecuteAsync
                            → success: format Matches as text, note if IsTruncated
  anything else         → "Error: tool '<name>' is not permitted."
```

The known/expected failure modes are wider than just "the path was outside the repo": `RepositoryPathResolver` throws `ArgumentException` on an empty path as well as `UnauthorizedAccessException` on traversal; `ReadFileTool` throws `FileNotFoundException`; `SearchRepositoryTool` throws `ArgumentException` on an empty query; and the model's `ArgumentsJson` itself might be malformed or missing the expected property (a `JsonException` or similar from parsing it). Rather than pattern-matching each exception type individually — which only guarantees "never throws" against the cases someone happened to think of — the per-tool dispatch (argument parsing **and** tool execution together) is wrapped in one `catch (Exception ex)` that returns `"Error: {ex.Message}"`. This is the actual guarantee ADR-0004's "malformed LLM actions" control calls for: any failure in this boundary becomes a safe observation, never a crash, regardless of cause.

`submit_findings` is **not** handled here — the agent loop special-cases it before calling `ToolPolicy`, since it's the loop's own termination signal, not a repository-access action. This keeps `ToolPolicy`'s responsibility to exactly one thing: authorize and execute genuine tool calls.

### `SecurityReviewAgent` (`src/SecurePrReviewer.Core/Agent/SecurityReviewAgent.cs`)

```csharp
public sealed class SecurityReviewAgent
{
    private const int MaxIterations = 5;

    public async Task<SecurityReview> ReviewAsync(ReviewRequest request, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new("system", SystemPrompt),
            new("user", $"Review the following diff for security issues:\n\n{request.Diff}")
        };
        var tools = new[] { ReadFileToolDefinition, SearchRepositoryToolDefinition, SubmitFindingsToolDefinition };

        for (var i = 0; i < MaxIterations; i++)
        {
            var response = await _llmClient.CompleteAsync(new ChatCompletionRequest(messages, tools), ct);

            if (response.ToolCalls is { Count: > 0 })
            {
                messages.Add(new ChatMessage("assistant", response.Content, response.ToolCalls));

                // Other tool calls in this batch are intentionally ignored once submit_findings is present.
                var submission = response.ToolCalls.FirstOrDefault(tc => tc.Name == "submit_findings");
                if (submission is not null)
                    return ParseFindings(submission.ArgumentsJson);

                foreach (var toolCall in response.ToolCalls)
                {
                    var observation = await _toolPolicy.ExecuteAsync(toolCall, ct);
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
}
```

`SubmitFindingsToolDefinition`'s JSON Schema is shaped to exactly mirror `SecurityReview`, so `ParseFindings(submission.ArgumentsJson)` is a direct `JsonSerializer.Deserialize<SecurityReview>` call — no separate mapping step:

```json
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
```

`ReadFileToolDefinition`/`SearchRepositoryToolDefinition` reuse the exact schemas already written for the `Program.cs` smoke-test demo (Task 4 of the tool-calling increment) rather than restating them — those two definitions move to live as private static fields on `SecurityReviewAgent`, alongside the new `SubmitFindingsToolDefinition`, removing the duplication. **`Program.cs` is rewired as part of this increment**: instead of the throwaway smoke test, it constructs a `LiteLlmClient`, a `ToolPolicy` (backed by `ReadFileTool`/`SearchRepositoryTool` rooted at the current directory or an argument), and a `SecurityReviewAgent`, then calls `ReviewAsync` with a hardcoded or argument-supplied diff and prints the resulting `SecurityReview`.

Two deliberate simplifications:
- **Multiple tool calls in one turn, one of which is `submit_findings`:** treated as terminal immediately; other calls in that batch are ignored (commented in the code, since it's a real invariant a reader could mistake for a bug).
- **Iteration limit reached without a submission:** throws `InvalidOperationException`, consistent with how the rest of the codebase signals "couldn't fulfill the contract" (`ReadFileTool`, `RepositoryPathResolver`, `LiteLlmClient` all throw rather than return a sentinel).

### Commenting convention

Confirmed via inspection: the codebase currently has **zero inline comments** in any method body across `src/` or `tests/` (the only exception, in `Program.cs`, flags that the smoke test is temporary — a lifecycle note, not code narration). This increment follows that convention with exactly one deliberate exception, justified by the same "non-obvious WHY" bar:

- The `submit_findings`-wins-and-ignores-rest line in `SecurityReviewAgent` (above).
- The path-traversal test in `ToolPolicyTests` (below) — a one-line comment above the method, not inside its body, stating that this is the concrete enforcement point for ADR-0004.

No other comments are added anywhere in this increment; naming and XML doc comments carry the rest, as established.

### Testing

**`ToolPolicyTests`** — real temp-directory repos, no mocking, matching `ReadFileToolTests`/`SearchRepositoryToolTests`:
- `read_file` tool call → success, returns file contents.
- `read_file` tool call with a path-traversal argument → caught, returns the safe rejection string, does **not** throw. This is the single most load-bearing test in the design:

  ```csharp
  // The concrete enforcement point for ADR-0004: the model's request is rejected by
  // the application, not merely refused by the model — the tool call never executes.
  [Fact]
  public async Task ExecuteAsync_ReadFilePathTraversal_ReturnsRejectionWithoutThrowing()
  {
      ...
  }
  ```
- `search_repository` tool call → success, formats matches.
- Unknown tool name (e.g. `"shell"`) → rejection string, no exception.

**`SecurityReviewAgentTests`** — a hand-rolled fake `ILlmClient` (same spirit as the existing `FakeHttpMessageHandler`), constructed with a scripted sequence of `ChatCompletionResponse`s to return across successive calls:
- Happy path: fake returns a `read_file` tool call, then `submit_findings` → agent returns the parsed `SecurityReview`.
- Fake never calls `submit_findings` within 5 iterations → `InvalidOperationException`.
- Fake requests an unauthorized tool → loop doesn't crash, continues (proves the loop's `ToolPolicy` wiring, not just `ToolPolicy`'s unit behavior).
- Fake returns plain text with no tool calls → agent nudges and continues rather than terminating early.

## Consequences

### Positive

- Completes the core reasoning loop described in ADR-0001, using only components already built and tested (`ILlmClient`, `ReadFileTool`, `SearchRepositoryTool`).
- `ToolPolicy` gives ADR-0003/0004's "the application remains responsible for validating, authorising and executing" a concrete, independently-testable home — matching the README architecture diagram's own "Tool Policy" box.
- No new abstraction layers beyond what ADR-0001 and the README diagram already name; the generic reusable "AgentLoop" shape from the original brainstorm was explicitly rejected as premature (no second consumer).

### Negative

- `ChatMessage`'s growing field count (now 4, two nullable) trades a small amount of type purity for avoiding a message-type hierarchy — consistent with the project's established preference, but worth revisiting if it grows further.
- No structured audit trail yet (see Deferred) — a `SECURITY EVENT` log line or `blockedActions` list is not produced by this increment, only a benign string fed back to the model.

## Deferred

- **Structured audit trail** (`blockedActions`, iteration count, tools used) as a first-class result alongside `SecurityReview` — explicitly flagged by the user during design review as the next thing to build once this loop lands. `ToolPolicy.ExecuteAsync`'s current string-only return is the natural seam to extend.
- Input guard (pre-scanning diff/repo content).
- Output guard (secret/sensitive-output redaction) — one of the three adversarial tests named in the project's original brief.
- The adversarial "killer demo" scenario itself (malicious `INSTRUCTIONS.md` in a demo repo, proving the loop end-to-end against a real injection attempt).
- `docs/architecture.md` and `docs/threat-model.md`, still linked from the README but not written.

## Reconsider When

If a second genuinely distinct agent or workflow emerges, revisit the "no generic AgentLoop abstraction" decision — until then, a second consumer is the bar for extracting one, not anticipated need.
