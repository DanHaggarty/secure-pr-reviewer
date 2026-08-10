# ADR-0005: Pass the PR diff as initial agent input

## Status

Accepted

## Context

The initial use case is always to review a pull request diff.

The diff is therefore required input for every review.

The model may need additional repository context after inspecting the diff, but it does not need to decide whether to retrieve the diff itself.

Exposing a `get_pr_diff` tool would add an unnecessary model decision and an extra execution step.

## Decision

Pass the repository path and PR diff into the application as the initial review request.

The C# application will create the agent state and include the diff in the first model interaction.

The model may then request additional context through allow-listed tools such as:

* `read_file`
* `search_repository`

The diff itself will not be exposed as an agent tool.

Conceptually:

```text
ReviewRequest(repoPath, diff)
        |
        v
SecurityReviewAgent
        |
        v
LLM reviews diff
   |          |
   | enough   | needs more context
   | context  v
   |      repository tools
   |          |
   +----------+
        |
        v
Structured security findings
```

## Consequences

### Positive

* Removes an unnecessary tool call.
* The initial workflow is explicit and easy to understand.
* The model receives the information it always needs immediately.
* Agent tool use is reserved for genuine decisions.
* Fewer moving parts in the initial implementation.

### Negative

* The initial request type is specific to PR review.
* Supporting other review targets may require extending the request abstraction later.

## Reconsider When

If the reviewer later supports multiple types of review targets, evolve the input abstraction rather than adding unnecessary retrieval tools.

For example, `ReviewRequest` could later accept a `ReviewTarget` representing a PR diff, commit, file or other review scope.
