using SecurePrReviewer.Core.Tools;

namespace SecurePrReviewer.Tests
{
    public class SearchRepositoryToolTests : IDisposable
    {
        private readonly string _repositoryRoot;

        public SearchRepositoryToolTests()
        {
            _repositoryRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_repositoryRoot);
        }

        [Fact]
        public async Task ExecuteAsync_MatchingLine_ReturnsRelativePathLineNumberAndText()
        {
            await File.WriteAllLinesAsync(
                Path.Combine(_repositoryRoot, "file.txt"),
                new[] { "first line", "hello world", "third line" });

            var tool = new SearchRepositoryTool(_repositoryRoot);

            var matches = await tool.ExecuteAsync("hello");

            var match = Assert.Single(matches);
            Assert.Equal("file.txt", match.RelativePath);
            Assert.Equal(2, match.LineNumber);
            Assert.Equal("hello world", match.LineText);
        }

        [Fact]
        public async Task ExecuteAsync_NestedFile_ReturnsMatch()
        {
            var nestedDir = Path.Combine(_repositoryRoot, "src", "nested");
            Directory.CreateDirectory(nestedDir);
            await File.WriteAllLinesAsync(
                Path.Combine(nestedDir, "file.txt"),
                new[] { "needle here" });

            var tool = new SearchRepositoryTool(_repositoryRoot);

            var matches = await tool.ExecuteAsync("needle");

            var match = Assert.Single(matches);
            Assert.Equal(
                Path.Combine("src", "nested", "file.txt"),
                match.RelativePath);
        }

        [Theory]
        [InlineData(".git")]
        [InlineData("bin")]
        [InlineData("obj")]
        public async Task ExecuteAsync_FileInIgnoredDirectory_IsNotSearched(string ignoredDirectoryName)
        {
            var ignoredDir = Path.Combine(_repositoryRoot, ignoredDirectoryName);
            Directory.CreateDirectory(ignoredDir);
            await File.WriteAllLinesAsync(
                Path.Combine(ignoredDir, "file.txt"),
                new[] { "needle here" });

            var tool = new SearchRepositoryTool(_repositoryRoot);

            var matches = await tool.ExecuteAsync("needle");

            Assert.Empty(matches);
        }

        [Fact]
        public async Task ExecuteAsync_NoMatches_ReturnsEmpty()
        {
            await File.WriteAllLinesAsync(
                Path.Combine(_repositoryRoot, "file.txt"),
                new[] { "nothing relevant here" });

            var tool = new SearchRepositoryTool(_repositoryRoot);

            var matches = await tool.ExecuteAsync("needle");

            Assert.Empty(matches);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExecuteAsync_EmptyQuery_Throws(string query)
        {
            var tool = new SearchRepositoryTool(_repositoryRoot);

            await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync(query));
        }

        [Fact]
        public async Task ExecuteAsync_MoreMatchesThanLimit_ReturnsBoundedResults()
        {
            var lines = Enumerable.Range(0, 150)
                .Select(i => $"needle {i}")
                .ToArray();
            await File.WriteAllLinesAsync(
                Path.Combine(_repositoryRoot, "file.txt"),
                lines);

            var tool = new SearchRepositoryTool(_repositoryRoot);

            var matches = await tool.ExecuteAsync("needle");

            Assert.Equal(100, matches.Count);
        }

        public void Dispose()
        {
            if (Directory.Exists(_repositoryRoot))
                Directory.Delete(_repositoryRoot, recursive: true);
        }
    }
}
