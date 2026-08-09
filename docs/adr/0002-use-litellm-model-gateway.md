# ADR-0002: Use LiteLLM as the model gateway

## Status

Accepted

## Context

The initial implementation uses a locally hosted open-weight model running through Ollama.

The agent should not depend directly on Ollama or a specific model provider.

A future implementation may use a different local model or a hosted provider.

## Decision

Route model requests through LiteLLM.

The application will address a logical model name through LiteLLM rather than calling Ollama directly.

Initial flow:

```text id="12e42n"
SecurityReviewAgent
        |
        v
     LiteLLM
        |
        v
      Ollama
        |
        v
qwen2.5-coder:7b
```

## Consequences

### Positive

* Agent code is decoupled from Ollama.
* Model configuration is external to the application.
* Providers or models can be changed without changing agent logic.
* The application can use an OpenAI-compatible API.
* Model infrastructure remains independently testable.

### Negative

* Adds another runtime component.
* Introduces additional configuration and networking.
* Some LiteLLM functionality is unnecessary for the initial scope.

## Reconsider When

Remove the gateway if the project permanently targets a single model runtime and the abstraction no longer provides meaningful value.
