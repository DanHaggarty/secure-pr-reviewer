using SecurePrReviewer.Core.Agent;
using SecurePrReviewer.Core.Llm;
using SecurePrReviewer.Core.Repository;
using SecurePrReviewer.Core.Review;
using SecurePrReviewer.Core.Tools;

namespace SecurePrReviewer.Tests
{
    public class SecurityReviewAgentTests : IDisposable
    {
        private readonly string _repositoryRoot;
        private readonly ToolPolicy _toolPolicy;

        public SecurityReviewAgentTests()
        {
            _repositoryRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_repositoryRoot);

            _toolPolicy = new ToolPolicy(
                new ReadFileTool(new RepositoryPathResolver(_repositoryRoot)),
                new SearchRepositoryTool(_repositoryRoot));
        }

        [Fact]
        public async Task ReviewAsync_ModelSubmitsFindings_ReturnsParsedReview()
        {
            const string findingsJson = """
                {"findings":[{"severity":"HIGH","title":"SQL Injection","description":"desc","location":"src/Foo.cs","recommendation":"use parameters"}]}
                """;
            var llmClient = new FakeLlmClient(
                new ChatCompletionResponse(null, new[] { new ToolCall("call_1", "submit_findings", findingsJson) }));
            var agent = new SecurityReviewAgent(llmClient, _toolPolicy);

            var review = await agent.ReviewAsync(new ReviewRequest(_repositoryRoot, "diff text"));

            var finding = Assert.Single(review.Findings);
            Assert.Equal("HIGH", finding.Severity);
            Assert.Equal("SQL Injection", finding.Title);
        }

        [Fact]
        public async Task ReviewAsync_ReadsFileThenSubmits_ReturnsParsedReview()
        {
            await File.WriteAllTextAsync(Path.Combine(_repositoryRoot, "file.txt"), "contents");

            const string findingsJson = """{"findings":[]}""";
            var llmClient = new FakeLlmClient(
                new ChatCompletionResponse(null, new[] { new ToolCall("call_1", "read_file", """{"path":"file.txt"}""") }),
                new ChatCompletionResponse(null, new[] { new ToolCall("call_2", "submit_findings", findingsJson) }));
            var agent = new SecurityReviewAgent(llmClient, _toolPolicy);

            var review = await agent.ReviewAsync(new ReviewRequest(_repositoryRoot, "diff text"));

            Assert.Empty(review.Findings);
        }

        [Fact]
        public async Task ReviewAsync_NeverSubmits_ThrowsAfterIterationLimit()
        {
            var responses = Enumerable.Range(0, 5)
                .Select(_ => new ChatCompletionResponse("still thinking"))
                .ToArray();
            var llmClient = new FakeLlmClient(responses);
            var agent = new SecurityReviewAgent(llmClient, _toolPolicy);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                agent.ReviewAsync(new ReviewRequest(_repositoryRoot, "diff text")));
        }

        [Fact]
        public async Task ReviewAsync_UnauthorizedToolRequested_DoesNotCrashAndEventuallySubmits()
        {
            const string findingsJson = """{"findings":[]}""";
            var llmClient = new FakeLlmClient(
                new ChatCompletionResponse(null, new[] { new ToolCall("call_1", "shell", """{"command":"rm -rf /"}""") }),
                new ChatCompletionResponse(null, new[] { new ToolCall("call_2", "submit_findings", findingsJson) }));
            var agent = new SecurityReviewAgent(llmClient, _toolPolicy);

            var review = await agent.ReviewAsync(new ReviewRequest(_repositoryRoot, "diff text"));

            Assert.Empty(review.Findings);
        }

        public void Dispose()
        {
            if (Directory.Exists(_repositoryRoot))
                Directory.Delete(_repositoryRoot, recursive: true);
        }
    }
}
