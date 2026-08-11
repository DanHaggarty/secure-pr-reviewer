using System.Net;
using System.Text.Json;
using SecurePrReviewer.App.Llm;
using SecurePrReviewer.Core.Llm;

namespace SecurePrReviewer.Tests
{
    public class LiteLlmClientTests
    {
        private const string ValidResponseJson =
            """{"choices":[{"message":{"role":"assistant","content":"pong"}}]}""";

        [Fact]
        public async Task CompleteAsync_SendsExpectedRequest()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponseJson);
            var client = new LiteLlmClient(new HttpClient(handler));

            await client.CompleteAsync(new ChatCompletionRequest(
                new[] { new ChatMessage("user", "hello") }));

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal(
                new Uri("http://localhost:4000/v1/chat/completions"),
                handler.LastRequest.RequestUri);

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            Assert.Equal("code-model", body.RootElement.GetProperty("model").GetString());
            var message = body.RootElement.GetProperty("messages")[0];
            Assert.Equal("user", message.GetProperty("role").GetString());
            Assert.Equal("hello", message.GetProperty("content").GetString());
        }

        [Fact]
        public async Task CompleteAsync_ParsesAssistantContentFromResponse()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponseJson);
            var client = new LiteLlmClient(new HttpClient(handler));

            var response = await client.CompleteAsync(new ChatCompletionRequest(
                new[] { new ChatMessage("user", "hello") }));

            Assert.Equal("pong", response.Content);
        }

        [Fact]
        public async Task CompleteAsync_UnsuccessfulStatusCode_ThrowsWithStatusAndBody()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "boom");
            var client = new LiteLlmClient(new HttpClient(handler));

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                client.CompleteAsync(new ChatCompletionRequest(
                    new[] { new ChatMessage("user", "hello") })));

            Assert.Contains("500", ex.Message);
            Assert.Contains("boom", ex.Message);
        }

        [Fact]
        public async Task CompleteAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponseJson);
            var client = new LiteLlmClient(new HttpClient(handler));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.CompleteAsync(
                    new ChatCompletionRequest(new[] { new ChatMessage("user", "hello") }),
                    cts.Token));
        }
    }
}
