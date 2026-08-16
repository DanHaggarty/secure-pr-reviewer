# ADR-0008: Publish review results to the pull request

## Status

Accepted

## Context

The reviewer currently prints its findings to the console only.

For the review to be useful in practice, its findings need to reach the pull request itself, where a human can see them.

A security review that identifies a high-severity issue should also signal that the change needs attention before merging, not just leave a note.

GitHub exposes two relevant mechanisms: a general PR comment (visible regardless of severity), and a formal PR review with a `REQUEST_CHANGES` event (the standard signal a human reviewer uses to say a change needs work).

## Decision

Define `IPrReviewPublisher`, separate from `IPrDiffFetcher` — fetching a diff and publishing a review are different responsibilities (read vs. write), and the interface segregation mirrors ADR-0007's reasoning.

`GitHubPrReviewPublisher` is the initial implementation.

Publishing always posts a general PR comment containing the full findings (or "No findings.") — this is the audit-trail/transparency layer, independent of severity.

If any finding has severity `HIGH`, publishing additionally submits a formal PR review with `event: REQUEST_CHANGES`, pointing back at the comment.

Flow:

```text
Program
    |
    v
IPrReviewPublisher
    |
    v
GitHubPrReviewPublisher
    |
    v
GitHub REST API (issue comment, and conditionally a PR review)
```

## Consequences

### Positive

* Review findings reach the pull request itself, not just the console.
* High-severity findings produce the standard "changes requested" signal a human reviewer would use, rather than a note that's easy to miss.
* Publishing is decoupled from GitHub specifically, consistent with ADR-0007.

### Negative

* `REQUEST_CHANGES` only actually blocks merging if the target repository's branch protection rules require it. This application can request changes; whether that prevents a merge is a repository setting outside this application's control.
* The token now needs write access to pull requests and issues, a materially larger permission grant than read-only diff fetching.

## Reconsider When

Remove the interface if the project permanently targets GitHub only and it never gains a second implementation, matching ADR-0007's reasoning.

Reconsider the HIGH-only blocking threshold if it proves too strict or too lax in practice.
