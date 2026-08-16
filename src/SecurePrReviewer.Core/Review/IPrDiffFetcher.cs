namespace SecurePrReviewer.Core.Review;

/// <summary>Fetches a pull request's diff from a source control host.</summary>
public interface IPrDiffFetcher
{
    /// <summary>Fetches the diff for the pull request referenced by <paramref name="prReference"/>.</summary>
    /// <param name="prReference">A reference identifying the pull request (e.g. its URL).</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The pull request's diff.</returns>
    /// <exception cref="ArgumentException"><paramref name="prReference"/> is not a valid reference for this fetcher's provider.</exception>
    /// <exception cref="HttpRequestException">The request to fetch the diff failed.</exception>
    Task<string> FetchDiffAsync(string prReference, CancellationToken cancellationToken = default);
}
