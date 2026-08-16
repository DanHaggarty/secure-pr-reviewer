using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SecurePrReviewer.Core.Review;

namespace SecurePrReviewer.App.GitHub;

/// <summary>Publishes a security review to a GitHub pull request, requesting changes if any finding is high severity.</summary>
public sealed class GitHubPrReviewPublisher : IPrReviewPublisher
{
    private static readonly Regex PrUrlPattern =
        new(@"^https://github\.com/([^/]+)/([^/]+)/pull/(\d+)/?$", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly string _token;

    /// <summary>Creates a publisher that authenticates requests with <paramref name="token"/>.</summary>
    /// <param name="httpClient">HTTP client used for requests; ownership stays with the caller.</param>
    /// <param name="token">A GitHub personal access token with write access to pull requests and issues.</param>
    public GitHubPrReviewPublisher(HttpClient httpClient, string token)
    {
        _httpClient = httpClient;
        _token = token;
    }

    /// <inheritdoc />
    public async Task PublishReviewAsync(
        string prUrl,
        SecurityReview review,
        CancellationToken cancellationToken = default)
    {
        var (owner, repo, number) = ParsePrUrl(prUrl);

        await SendAsync(
            $"https://api.github.com/repos/{owner}/{repo}/issues/{number}/comments",
            new { body = FormatComment(review) },
            cancellationToken);

        if (review.Findings.Any(f => f.Severity == "HIGH"))
        {
            await SendAsync(
                $"https://api.github.com/repos/{owner}/{repo}/pulls/{number}/reviews",
                new { body = "Security review identified high-severity issues — see comment above.", @event = "REQUEST_CHANGES" },
                cancellationToken);
        }
    }

    private async Task SendAsync(string uri, object payload, CancellationToken cancellationToken)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SecurePrReviewer", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"GitHub request failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}");
    }

    private static string FormatComment(SecurityReview review)
    {
        if (review.Findings.Count == 0)
            return "No findings.";

        var sections = review.Findings.Select(f =>
            $"**{f.Severity} — {f.Title}**{Environment.NewLine}{Environment.NewLine}" +
            $"{f.Description}{Environment.NewLine}{Environment.NewLine}" +
            $"`{f.Location}`{Environment.NewLine}{Environment.NewLine}" +
            $"{f.Recommendation}");

        return string.Join($"{Environment.NewLine}{Environment.NewLine}---{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private static (string Owner, string Repo, int Number) ParsePrUrl(string prUrl)
    {
        var match = PrUrlPattern.Match(prUrl);
        if (!match.Success)
            throw new ArgumentException($"'{prUrl}' is not a valid GitHub pull request URL.", nameof(prUrl));

        return (
            match.Groups[1].Value,
            match.Groups[2].Value,
            int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
    }
}
