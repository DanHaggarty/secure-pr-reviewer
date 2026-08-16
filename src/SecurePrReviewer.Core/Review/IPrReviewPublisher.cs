namespace SecurePrReviewer.Core.Review;

/// <summary>Publishes a completed security review to its source pull request.</summary>
public interface IPrReviewPublisher
{
    /// <summary>Publishes <paramref name="review"/> to the pull request referenced by <paramref name="prReference"/>.</summary>
    /// <param name="prReference">A reference identifying the pull request (e.g. its URL).</param>
    /// <param name="review">The completed security review.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <exception cref="ArgumentException"><paramref name="prReference"/> is not a valid reference for this publisher's provider.</exception>
    /// <exception cref="HttpRequestException">The request to publish the review failed.</exception>
    Task PublishReviewAsync(string prReference, SecurityReview review, CancellationToken cancellationToken = default);
}
