# LLM Tool-Calling Design

## Status

Proposed

## Context

The project has a working `ILlmClient`/`LiteLlmClient` (Core + App) that can send a plain chat message through LiteLLM to the Ollama-hosted model and get a text reply back, proven with a "pong" smoke test in `Program.cs`.

The next milestone on the roadmap is the agent loop described in ADR-0001 (explicit reason → request tool → validate → execute → observe → reason cycle) using the allow-listed tools from ADR-0003 (`read_file`, `search_repository`). The loop's entire job is authorizing and executing model-requested tool calls — which means the LLM client must first be able to (a) tell the model what tools are available and (b) recognize when the model wants to call one, before any loop can be built around it.

This spec covers only that prerequisite: extending the LLM client to carry tool definitions in a request and surface tool-call requests in a response. It deliberately stops short of executing tools or continuing the conversation with results — that orchestration is the agent loop's job and is out of scope here.

## Decision

Extend the existing chat completion types and `LiteLlmClient` to support the OpenAI-compatible tool-calling wire protocol, and prove it works with a console demo targeting the real `read_file`/`search_repository` tool schemas.

### Core additions (`src/SecurePrReviewer.Core/Llm/ChatCompletion.cs`)

Provider-agnostic; no JSON attributes, no OpenAI-specific concepts.

```csharp
public sealed record ToolDefinition(string Name, string Description, string ParametersSchemaJson);

public sealed record ToolCall(string Id, string Name, string ArgumentsJson);
```

- `ToolDefinition.ParametersSchemaJson` is an opaque JSON Schema string. Core never parses or understands it — the caller (for now, `Program.cs`; later the agent loop's tool registry) hand-writes the schema per tool. This avoids building a structured schema-modeling layer for tools that each take a single string parameter today.
- `ToolCall.Id` is captured now even though nothing consumes it yet, because it's present on the wire and will be required once the agent loop round-trips tool results back to the model (the OpenAI protocol requires echoing `tool_call_id` on the follow-up message).

`ChatCompletionRequest` and `ChatCompletionResponse` change:

```csharp
public sealed record ChatCompletionRequest(
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<ToolDefinition>? Tools = null);

public sealed record ChatCompletionResponse(
    string? Content,
    IReadOnlyList<ToolCall>? ToolCalls = null);
```

`Content` becomes nullable — mirrors the real OpenAI message shape, where `content` is `null` when the model responds with `tool_calls` instead of text.

### App additions (`src/SecurePrReviewer.App/Llm/LiteLlmClient.cs`)

The private LiteLLM-specific DTOs grow to match the OpenAI tool-calling wire format:

Request adds a `tools` array:
```json
{
  "model": "code-model",
  "messages": [...],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "read_file",
        "description": "...",
        "parameters": { /* raw JSON Schema, passed through as-is */ }
      }
    }
  ]
}
```

Response message adds a `tool_calls` array:
```json
{
  "choices": [{
    "message": {
      "role": "assistant",
      "content": null,
      "tool_calls": [{
        "id": "call_abc",
        "type": "function",
        "function": { "name": "read_file", "arguments": "{\"path\":\"Program.cs\"}" }
      }]
    }
  }]
}
```

`LiteLlmClient` maps `ChatCompletionRequest.Tools` into this shape when present. The `tools` field is omitted entirely (not sent as an empty array) whenever `Tools` is null **or** empty, since some models/gateways treat an empty `tools` array differently from "no tools". `LiteLlmClient` also maps a response's `tool_calls` into `ChatCompletionResponse.ToolCalls`.

The existing "empty response" guard changes: currently it requires non-null/non-empty `Content` and throws otherwise. It must now accept a message with *either* non-empty `Content` or a non-empty `ToolCalls` list, throwing `InvalidOperationException` only when both are absent.

### Program.cs demo

Extend the existing smoke-test flow (still throwaway, not the agent loop): define the two ADR-0003 tool schemas as literal JSON Schema strings (`read_file` takes `{ path: string }`, `search_repository` takes `{ query: string }`), send a prompt engineered to trigger a call (e.g. "Use the read_file tool to read Program.cs"), and print either `response.Content` (if the model answered directly) or each requested tool call's name and arguments (if the model chose to call a tool).

## Data Flow

```
Program.cs
  builds ChatCompletionRequest { Messages, Tools: [read_file, search_repository] }
        |
        v
LiteLlmClient.CompleteAsync
  serializes Tools into OpenAI "tools" shape
  POSTs to http://localhost:4000/v1/chat/completions
        |
        v
LiteLLM -> Ollama -> qwen2.5-coder:7b
  model responds with either:
    - content (plain text answer), or
    - tool_calls (model wants to invoke a tool)
        |
        v
LiteLlmClient
  parses response into ChatCompletionResponse { Content?, ToolCalls? }
        |
        v
Program.cs prints Content, or each ToolCall's Name + ArgumentsJson
```

## Testing

Extend `tests/SecurePrReviewer.Tests/LiteLlmClientTests.cs` (same `FakeHttpMessageHandler` pattern already established):

1. **Request includes tools when supplied** — assert the serialized request body's `tools` array matches the given `ToolDefinition`s (name, description, parameters passed through verbatim).
2. **Tool-call response is parsed** — canned response containing `tool_calls`; assert `ChatCompletionResponse.ToolCalls` has the correct `Id`/`Name`/`ArgumentsJson`, and `Content` is null.
3. **Plain-content response still parses** (regression) — existing canned "pong" response still yields `Content == "pong"` and empty/null `ToolCalls`.
4. **Neither content nor tool_calls throws** — canned response with an empty message object; assert `InvalidOperationException`.
5. **No tools supplied omits the field** — when `Tools` is null (or empty), assert the serialized request body has no `tools` property at all.

The 4 existing tests (request shape, response parsing, unsuccessful status code, cancellation propagation) continue to apply unchanged — this is an additive change to the request/response shape, not a rewrite of the HTTP/error-handling mechanics.

## Consequences

### Positive

- Proves the tool-calling mechanism against the real tool schemas the agent loop will use next, rather than throwaway code that gets discarded.
- `ChatCompletionResponse.Content` becoming nullable is a small, well-contained breaking change caught immediately by the compiler at the one call site (`Program.cs`).
- No new packages, no schema-modeling layer, no round-trip/continuation logic — stays scoped to "can the client offer tools and recognize a tool-call request."

### Negative

- `ToolCall.Id` and the nullable `Content` add a small amount of surface area that isn't exercised end-to-end until the agent loop exists to consume it.
- Hand-written JSON Schema strings in `Program.cs` are throwaway; the agent loop will need a real place for tool schemas to live (likely alongside a `ToolDefinition` factory per tool, or a small registry) — deferred to that increment.

## Out of Scope

- Executing a requested tool call.
- Sending a tool's result back to the model and continuing the conversation (multi-turn round-trip).
- Tool authorization/allow-listing logic (ADR-0003's enforcement point) — the agent loop's responsibility.
- A `ChatMessage` "tool" role or `ToolCallId` field for representing tool results in history — not needed until round-tripping is built.

## Reconsider When

If a future tool needs a genuinely structured parameter schema (e.g. nested objects, enums with descriptions) such that hand-writing JSON Schema strings becomes error-prone, introduce a small schema-builder type at that point rather than upfront.
