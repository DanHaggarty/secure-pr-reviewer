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

        private const string ToolCallResponseJson =
            """{"choices":[{"message":{"role":"assistant","content":null,"tool_calls":[{"id":"call_abc","type":"function","function":{"name":"read_file","arguments":"{\"path\":\"Program.cs\"}"}}]}}]}""";

        private const string EmptyMessageResponseJson =
            """{"choices":[{"message":{"role":"assistant","content":null}}]}""";

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
        public async Task CompleteAsync_IncludesToolsWhenSupplied()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponseJson);
            var client = new LiteLlmClient(new HttpClient(handler));

            await client.CompleteAsync(new ChatCompletionRequest(
                new[] { new ChatMessage("user", "hello") },
                new[]
                {
                    new ToolDefinition(
                        "read_file",
                        "Reads a file.",
                        """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}""")
                }));

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            var tool = body.RootElement.GetProperty("tools")[0];
            Assert.Equal("function", tool.GetProperty("type").GetString());
            var function = tool.GetProperty("function");
            Assert.Equal("read_file", function.GetProperty("name").GetString());
            Assert.Equal("Reads a file.", function.GetProperty("description").GetString());
            Assert.Equal(
                "string",
                function.GetProperty("parameters").GetProperty("properties")
                    .GetProperty("path").GetProperty("type").GetString());
        }

        [Fact]
        public async Task CompleteAsync_NoToolsSupplied_OmitsToolsField()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponseJson);
            var client = new LiteLlmClient(new HttpClient(handler));

            await client.CompleteAsync(new ChatCompletionRequest(
                new[] { new ChatMessage("user", "hello") }));

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            Assert.False(body.RootElement.TryGetProperty("tools", out _));
        }

        [Fact]
        public async Task CompleteAsync_SerializesToolCallsOnHistoryMessage()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponseJson);
            var client = new LiteLlmClient(new HttpClient(handler));

            var assistantMessage = new ChatMessage(
                "assistant",
                null,
                new[] { new ToolCall("call_abc", "read_file", """{"path":"Program.cs"}""") });

            await client.CompleteAsync(new ChatCompletionRequest(
                new[] { new ChatMessage("user", "hello"), assistantMessage }));

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            var message = body.RootElement.GetProperty("messages")[1];
            var toolCall = message.GetProperty("tool_calls")[0];
            Assert.Equal("call_abc", toolCall.GetProperty("id").GetString());
            Assert.Equal("function", toolCall.GetProperty("type").GetString());
            Assert.Equal("read_file", toolCall.GetProperty("function").GetProperty("name").GetString());
            Assert.Equal(
                """{"path":"Program.cs"}""",
                toolCall.GetProperty("function").GetProperty("arguments").GetString());
        }

        [Fact]
        public async Task CompleteAsync_SerializesToolCallIdOnToolResultMessage()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponseJson);
            var client = new LiteLlmClient(new HttpClient(handler));

            var toolResultMessage = new ChatMessage("tool", "file contents", ToolCallId: "call_abc");

            await client.CompleteAsync(new ChatCompletionRequest(
                new[] { new ChatMessage("user", "hello"), toolResultMessage }));

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            var message = body.RootElement.GetProperty("messages")[1];
            Assert.Equal("call_abc", message.GetProperty("tool_call_id").GetString());
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
        public async Task CompleteAsync_ParsesToolCallsFromResponse()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ToolCallResponseJson);
            var client = new LiteLlmClient(new HttpClient(handler));

            var response = await client.CompleteAsync(new ChatCompletionRequest(
                new[] { new ChatMessage("user", "hello") }));

            Assert.Null(response.Content);
            var toolCall = Assert.Single(response.ToolCalls!);
            Assert.Equal("call_abc", toolCall.Id);
            Assert.Equal("read_file", toolCall.Name);
            Assert.Equal("""{"path":"Program.cs"}""", toolCall.ArgumentsJson);
        }

        [Fact]
        public async Task CompleteAsync_NeitherContentNorToolCalls_Throws()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, EmptyMessageResponseJson);
            var client = new LiteLlmClient(new HttpClient(handler));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.CompleteAsync(new ChatCompletionRequest(
                    new[] { new ChatMessage("user", "hello") })));
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
