# ADR-0007: Abstract the pull request diff source

## Status

Accepted

## Context

The reviewer currently fetches a pull request's diff directly from GitHub's REST API.

This ties diff retrieval to one specific provider.

The project is named SecurePrReviewer, not GitHubPrReviewer — GitHub was never intended to be the only supported source.

Other hosting providers (GitLab, Bitbucket, Azure DevOps) expose an equivalent operation: given a pull/merge request reference and a token, return its diff.

This mirrors ADR-0002's reasoning for routing model calls through LiteLLM rather than calling Ollama directly: the application should not depend on one specific provider where a documented, likely-to-vary seam exists.

## Decision

Define an interface for fetching a pull request's diff, independent of provider.

The application will depend on this interface rather than a concrete GitHub client directly.

`GitHubPrDiffFetcher` becomes the initial implementation.

Flow:

```text
Program
    |
    v
IPrDiffFetcher
    |
    v
GitHubPrDiffFetcher
    |
    v
GitHub REST API
```

## Consequences

### Positive

* Diff retrieval is decoupled from GitHub specifically.
* Other providers (GitLab, Bitbucket, Azure DevOps) can be supported without changing the agent or its callers.
* Consistent with how model access is already abstracted (ADR-0002).

### Negative

* Adds an interface with a single implementation initially.
* Provider-specific concepts (PR references, tokens, diff formats) still need per-provider translation behind the interface.

## Reconsider When

Remove the abstraction if the project permanently targets GitHub only and the interface never gains a second implementation.
