using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using SecurePrReviewer.Core.Review;

namespace SecurePrReviewer.App.GitHub;

/// <summary>Fetches a pull request's diff from the GitHub REST API.</summary>
public sealed class GitHubPrDiffFetcher : IPrDiffFetcher
{
    private static readonly Regex PrUrlPattern =
        new(@"^https://github\.com/([^/]+)/([^/]+)/pull/(\d+)/?$", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly string _token;

    /// <summary>Creates a fetcher that authenticates requests with <paramref name="token"/>.</summary>
    /// <param name="httpClient">HTTP client used for requests; ownership stays with the caller.</param>
    /// <param name="token">A GitHub personal access token with access to the target repository.</param>
    public GitHubPrDiffFetcher(HttpClient httpClient, string token)
    {
        _httpClient = httpClient;
        _token = token;
    }

    /// <inheritdoc />
    public async Task<string> FetchDiffAsync(string prUrl, CancellationToken cancellationToken = default)
    {
        var (owner, repo, number) = ParsePrUrl(prUrl);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{owner}/{repo}/pulls/{number}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3.diff"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SecurePrReviewer", "1.0"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"GitHub request failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}");

        return body;
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
