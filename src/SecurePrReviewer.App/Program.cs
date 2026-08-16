using SecurePrReviewer.App.GitHub;
using SecurePrReviewer.App.Llm;
using SecurePrReviewer.Core.Agent;
using SecurePrReviewer.Core.Repository;
using SecurePrReviewer.Core.Review;
using SecurePrReviewer.Core.Tools;

var repoPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var diffSource = args.Length > 1 ? args[1] : null;

using var httpClient = new HttpClient();

if (diffSource is not null)
    Console.WriteLine("Fetching diff...");

var diff = diffSource is not null ? await ResolveDiffAsync(diffSource, httpClient) : "No diff supplied.";

var llmClient = new LiteLlmClient(httpClient);
var toolPolicy = new ToolPolicy(
    new ReadFileTool(new RepositoryPathResolver(repoPath)),
    new SearchRepositoryTool(repoPath));
var agent = new SecurityReviewAgent(llmClient, toolPolicy);

try
{
    Console.WriteLine("Reviewing (this can take a minute or more, depending on the model and how many tools it uses)...");
    var review = await agent.ReviewAsync(new ReviewRequest(repoPath, diff));

    if (review.Findings.Count == 0)
    {
        Console.WriteLine("No findings.");
    }
    else
    {
        foreach (var finding in review.Findings)
        {
            Console.WriteLine($"{finding.Severity} — {finding.Title}");
            Console.WriteLine(finding.Description);
            Console.WriteLine(finding.Location);
            Console.WriteLine(finding.Recommendation);
            Console.WriteLine();
        }
    }

    if (diffSource is not null && diffSource.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? throw new InvalidOperationException("GITHUB_TOKEN environment variable is required to publish the review.");

        Console.WriteLine("Publishing review to pull request...");
        IPrReviewPublisher publisher = new GitHubPrReviewPublisher(httpClient, token);
        await publisher.PublishReviewAsync(diffSource, review);
        Console.WriteLine("Published review to pull request.");
    }
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Request failed: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Review did not complete: {ex.Message}");
}

static async Task<string> ResolveDiffAsync(string diffSource, HttpClient httpClient)
{
    if (!diffSource.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        return await File.ReadAllTextAsync(diffSource);

    var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
        ?? throw new InvalidOperationException("GITHUB_TOKEN environment variable is required to fetch a PR diff.");

    IPrDiffFetcher prDiffFetcher = new GitHubPrDiffFetcher(httpClient, token);
    return await prDiffFetcher.FetchDiffAsync(diffSource);
}
