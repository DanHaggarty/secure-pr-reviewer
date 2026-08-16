using SecurePrReviewer.App.Llm;
using SecurePrReviewer.Core.Agent;
using SecurePrReviewer.Core.Repository;
using SecurePrReviewer.Core.Review;
using SecurePrReviewer.Core.Tools;

var repoPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var diff = args.Length > 1 ? await File.ReadAllTextAsync(args[1]) : "No diff supplied.";

using var httpClient = new HttpClient();
var llmClient = new LiteLlmClient(httpClient);
var toolPolicy = new ToolPolicy(
    new ReadFileTool(new RepositoryPathResolver(repoPath)),
    new SearchRepositoryTool(repoPath));
var agent = new SecurityReviewAgent(llmClient, toolPolicy);

try
{
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
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"LLM request failed: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Review did not complete: {ex.Message}");
}
