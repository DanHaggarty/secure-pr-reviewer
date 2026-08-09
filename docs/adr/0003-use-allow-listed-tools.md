# ADR-0003: Use allow-listed tools instead of arbitrary execution

## Status

Accepted

## Context

The agent needs additional repository context when reviewing a pull request.

Giving an LLM unrestricted filesystem or shell access would significantly increase the impact of prompt injection, model errors or malicious repository content.

The agent only needs a small set of repository operations for the initial workflow.

## Decision

Expose a small set of explicit, allow-listed tools.

Initial tools:

* `read_file`
* `search_repository`

The model may request a tool call, but the application remains responsible for validating, authorising and executing it.

Generic shell execution will not be exposed.

## Consequences

### Positive

* Model capabilities are tightly bounded.
* Tool behaviour can be tested independently.
* Path restrictions can be enforced deterministically.
* Prompt injection cannot create new capabilities.
* Tool usage is auditable.

### Negative

* New capabilities require new tools.
* Some repository operations may require additional implementation work.
* The agent is intentionally less autonomous.

## Reconsider When

Add tools only when a concrete review scenario requires them.

Any new tool must define its own validation and security boundary before being exposed to the model.
