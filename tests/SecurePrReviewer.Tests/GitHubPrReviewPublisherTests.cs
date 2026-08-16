using System.Net;
using System.Text.Json;
using SecurePrReviewer.App.GitHub;
using SecurePrReviewer.Core.Review;

namespace SecurePrReviewer.Tests
{
    public class GitHubPrReviewPublisherTests
    {
        private const string PrUrl = "https://github.com/DanHaggarty/secure-pr-reviewer/pull/123";

        [Fact]
        public async Task PublishReviewAsync_NoFindings_PostsCommentOnly()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.Created, "{}");
            var publisher = new GitHubPrReviewPublisher(new HttpClient(handler), "test-token");
            var review = new SecurityReview(Array.Empty<SecurityFinding>());

            await publisher.PublishReviewAsync(PrUrl, review);

            var request = Assert.Single(handler.Requests);
            Assert.Equal(
                new Uri("https://api.github.com/repos/DanHaggarty/secure-pr-reviewer/issues/123/comments"),
                request.RequestUri);
            using var body = JsonDocument.Parse(handler.RequestBodies[0]!);
            Assert.Equal("No findings.", body.RootElement.GetProperty("body").GetString());
        }

        [Fact]
        public async Task PublishReviewAsync_OnlyMediumFindings_PostsCommentOnly()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.Created, "{}");
            var publisher = new GitHubPrReviewPublisher(new HttpClient(handler), "test-token");
            var review = new SecurityReview(new[]
            {
                new SecurityFinding("MEDIUM", "Weak hashing", "desc", "src/Foo.cs", "use bcrypt")
            });

            await publisher.PublishReviewAsync(PrUrl, review);

            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task PublishReviewAsync_HighFinding_PostsCommentAndRequestsChanges()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.Created, "{}");
            var publisher = new GitHubPrReviewPublisher(new HttpClient(handler), "test-token");
            var review = new SecurityReview(new[]
            {
                new SecurityFinding("HIGH", "SQL Injection", "desc", "src/Foo.cs", "use parameters")
            });

            await publisher.PublishReviewAsync(PrUrl, review);

            Assert.Equal(2, handler.Requests.Count);

            Assert.Equal(
                new Uri("https://api.github.com/repos/DanHaggarty/secure-pr-reviewer/issues/123/comments"),
                handler.Requests[0].RequestUri);
            using var commentBody = JsonDocument.Parse(handler.RequestBodies[0]!);
            Assert.Contains("SQL Injection", commentBody.RootElement.GetProperty("body").GetString());

            Assert.Equal(
                new Uri("https://api.github.com/repos/DanHaggarty/secure-pr-reviewer/pulls/123/reviews"),
                handler.Requests[1].RequestUri);
            using var reviewBody = JsonDocument.Parse(handler.RequestBodies[1]!);
            Assert.Equal("REQUEST_CHANGES", reviewBody.RootElement.GetProperty("event").GetString());
        }

        [Fact]
        public async Task PublishReviewAsync_UnsuccessfulStatusCode_ThrowsWithStatusAndBody()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.Forbidden, "Insufficient permissions");
            var publisher = new GitHubPrReviewPublisher(new HttpClient(handler), "test-token");
            var review = new SecurityReview(Array.Empty<SecurityFinding>());

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                publisher.PublishReviewAsync(PrUrl, review));

            Assert.Contains("403", ex.Message);
            Assert.Contains("Insufficient permissions", ex.Message);
        }

        [Fact]
        public async Task PublishReviewAsync_InvalidPrUrl_ThrowsArgumentException()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.Created, "{}");
            var publisher = new GitHubPrReviewPublisher(new HttpClient(handler), "test-token");
            var review = new SecurityReview(Array.Empty<SecurityFinding>());

            await Assert.ThrowsAsync<ArgumentException>(() =>
                publisher.PublishReviewAsync("not-a-url", review));
        }
    }
}
