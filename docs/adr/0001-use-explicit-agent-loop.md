# ADR-0001: Use an explicit agent loop

## Status

Accepted

## Context

The initial workflow has one responsibility: review a pull request for potential security issues.

The agent may inspect additional repository context by requesting tools, then continue reasoning until it can return a structured review.

Agent orchestration frameworks such as LangGraph were considered.

The workflow is currently small enough that introducing a graph framework would add abstraction without providing much value.

## Decision

Implement the initial agent loop directly in C#.

The application will explicitly manage:

* agent state
* model calls
* tool requests
* tool authorisation
* tool observations
* termination conditions
* iteration limits

## Consequences

### Positive

* Control flow remains easy to inspect.
* Tool authorisation is explicit.
* Security boundaries are easier to reason about.
* Termination behaviour is visible.
* Fewer framework dependencies.
* The implementation remains small enough to understand end to end.

### Negative

* The project owns its orchestration code.
* More complex workflows may eventually require additional infrastructure.

## Reconsider When

Reconsider an orchestration framework if the workflow gains:

* multiple genuinely distinct agents
* complex branching
* parallel execution
* durable state
* human approval stages
* resumable workflows
