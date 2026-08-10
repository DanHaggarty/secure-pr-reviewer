\# ADR-0006: Propagate cancellation tokens through async operations



\## Status



Accepted



\## Context



The reviewer performs potentially long-running asynchronous work, including model calls, repository reads and searches.



The initial console implementation may not expose a user-facing cancel action, but cancellation is likely to be required by future hosting environments such as an API, CLI timeout or explicit review abort.



Adding cancellation support later would require changing method signatures throughout the call chain.



\## Decision



Accept and propagate `CancellationToken` through asynchronous application boundaries from the initial implementation.



For example:



```csharp

Task<SecurityReview> ReviewAsync(

&#x20;   ReviewRequest request,

&#x20;   CancellationToken cancellationToken = default);

