using System.Net;
using SecurePrReviewer.App.GitHub;

namespace SecurePrReviewer.Tests
{
    public class GitHubPrDiffFetcherTests
    {
        [Fact]
        public async Task FetchDiffAsync_SendsExpectedRequest()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "diff --git a/Foo.cs b/Foo.cs");
            var fetcher = new GitHubPrDiffFetcher(new HttpClient(handler), "test-token");

            await fetcher.FetchDiffAsync("https://github.com/DanHaggarty/secure-pr-reviewer/pull/123");

            Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
            Assert.Equal(
                new Uri("https://api.github.com/repos/DanHaggarty/secure-pr-reviewer/pulls/123"),
                handler.LastRequest.RequestUri);
            Assert.Equal("Bearer test-token", handler.LastRequest.Headers.Authorization!.ToString());
            Assert.Contains(
                handler.LastRequest.Headers.Accept,
                h => h.MediaType == "application/vnd.github.v3.diff");
        }

        [Fact]
        public async Task FetchDiffAsync_ReturnsResponseBody()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "diff --git a/Foo.cs b/Foo.cs");
            var fetcher = new GitHubPrDiffFetcher(new HttpClient(handler), "test-token");

            var diff = await fetcher.FetchDiffAsync("https://github.com/DanHaggarty/secure-pr-reviewer/pull/123");

            Assert.Equal("diff --git a/Foo.cs b/Foo.cs", diff);
        }

        [Fact]
        public async Task FetchDiffAsync_UnsuccessfulStatusCode_ThrowsWithStatusAndBody()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, "Not Found");
            var fetcher = new GitHubPrDiffFetcher(new HttpClient(handler), "test-token");

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                fetcher.FetchDiffAsync("https://github.com/DanHaggarty/secure-pr-reviewer/pull/123"));

            Assert.Contains("404", ex.Message);
            Assert.Contains("Not Found", ex.Message);
        }

        [Theory]
        [InlineData("not-a-url")]
        [InlineData("https://example.com/owner/repo/pull/123")]
        [InlineData("https://github.com/owner/repo/issues/123")]
        public async Task FetchDiffAsync_InvalidPrUrl_ThrowsArgumentException(string invalidUrl)
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "");
            var fetcher = new GitHubPrDiffFetcher(new HttpClient(handler), "test-token");

            await Assert.ThrowsAsync<ArgumentException>(() => fetcher.FetchDiffAsync(invalidUrl));
        }
    }
}
