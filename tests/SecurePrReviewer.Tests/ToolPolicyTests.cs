using SecurePrReviewer.Core.Agent;
using SecurePrReviewer.Core.Llm;
using SecurePrReviewer.Core.Repository;
using SecurePrReviewer.Core.Tools;

namespace SecurePrReviewer.Tests
{
    public class ToolPolicyTests : IDisposable
    {
        private readonly string _repositoryRoot;
        private readonly ToolPolicy _policy;

        public ToolPolicyTests()
        {
            _repositoryRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_repositoryRoot);

            _policy = new ToolPolicy(
                new ReadFileTool(new RepositoryPathResolver(_repositoryRoot)),
                new SearchRepositoryTool(_repositoryRoot));
        }

        [Fact]
        public async Task ExecuteAsync_ReadFile_ReturnsFileContents()
        {
            await File.WriteAllTextAsync(Path.Combine(_repositoryRoot, "file.txt"), "hello world");
            var toolCall = new ToolCall("call_1", "read_file", """{"path":"file.txt"}""");

            var observation = await _policy.ExecuteAsync(toolCall);

            Assert.Equal("hello world", observation);
        }

        // The concrete enforcement point for ADR-0004: the model's request is rejected by
        // the application, not merely refused by the model — the tool call never executes.
        [Fact]
        public async Task ExecuteAsync_ReadFilePathTraversal_ReturnsRejectionWithoutThrowing()
        {
            var toolCall = new ToolCall("call_1", "read_file", """{"path":"../secrets.txt"}""");

            var observation = await _policy.ExecuteAsync(toolCall);

            Assert.StartsWith("Error:", observation);
        }

        [Fact]
        public async Task ExecuteAsync_SearchRepository_ReturnsFormattedMatches()
        {
            await File.WriteAllLinesAsync(
                Path.Combine(_repositoryRoot, "file.txt"),
                new[] { "hello world" });
            var toolCall = new ToolCall("call_1", "search_repository", """{"query":"hello"}""");

            var observation = await _policy.ExecuteAsync(toolCall);

            Assert.Contains("file.txt:1: hello world", observation);
        }

        [Fact]
        public async Task ExecuteAsync_UnknownTool_ReturnsRejectionWithoutThrowing()
        {
            var toolCall = new ToolCall("call_1", "shell", """{"command":"rm -rf /"}""");

            var observation = await _policy.ExecuteAsync(toolCall);

            Assert.Equal("Error: tool 'shell' is not permitted.", observation);
        }

        [Fact]
        public async Task ExecuteAsync_SearchRepositoryTruncated_ObservationNudgesModelToRefine()
        {
            var lines = Enumerable.Range(0, 60).Select(i => $"needle {i}").ToArray();
            await File.WriteAllLinesAsync(Path.Combine(_repositoryRoot, "file.txt"), lines);
            var toolCall = new ToolCall("call_1", "search_repository", """{"query":"needle"}""");

            var observation = await _policy.ExecuteAsync(toolCall);

            Assert.Contains("results truncated", observation);
            Assert.Contains("refine your search query", observation);
        }

        public void Dispose()
        {
            if (Directory.Exists(_repositoryRoot))
                Directory.Delete(_repositoryRoot, recursive: true);
        }
    }
}
