namespace SecurePrReviewer.Core.Review;

/// <summary>A request to review a pull request diff for security issues.</summary>
/// <param name="RepoPath">Path to the repository the diff belongs to.</param>
/// <param name="Diff">The pull request diff to review.</param>
public sealed record ReviewRequest(string RepoPath, string Diff);

/// <summary>A single security issue identified during a review.</summary>
/// <param name="Severity">The finding's severity (e.g. "HIGH", "MEDIUM", "LOW").</param>
/// <param name="Title">A short summary of the issue.</param>
/// <param name="Description">Why this is a security concern.</param>
/// <param name="Location">Where the issue was found (e.g. a file path).</param>
/// <param name="Recommendation">How to address the issue.</param>
public sealed record SecurityFinding(
    string Severity,
    string Title,
    string Description,
    string Location,
    string Recommendation);

/// <summary>The completed result of a security review.</summary>
/// <param name="Findings">The security issues identified, if any.</param>
public sealed record SecurityReview(IReadOnlyList<SecurityFinding> Findings);
