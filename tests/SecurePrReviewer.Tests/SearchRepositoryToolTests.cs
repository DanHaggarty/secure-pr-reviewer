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

            var result = await tool.ExecuteAsync("hello");

            var match = Assert.Single(result.Matches);
            Assert.Equal("file.txt", match.RelativePath);
            Assert.Equal(2, match.LineNumber);
            Assert.Equal("hello world", match.LineText);
            Assert.False(result.IsTruncated);
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

            var result = await tool.ExecuteAsync("needle");

            var match = Assert.Single(result.Matches);
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

            var result = await tool.ExecuteAsync("needle");

            Assert.Empty(result.Matches);
        }

        [Fact]
        public async Task ExecuteAsync_NoMatches_ReturnsEmpty()
        {
            await File.WriteAllLinesAsync(
                Path.Combine(_repositoryRoot, "file.txt"),
                new[] { "nothing relevant here" });

            var tool = new SearchRepositoryTool(_repositoryRoot);

            var result = await tool.ExecuteAsync("needle");

            Assert.Empty(result.Matches);
            Assert.False(result.IsTruncated);
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
        public async Task ExecuteAsync_FewerMatchesThanLimit_IsNotTruncated()
        {
            await WriteNeedleLines(49);

            var tool = new SearchRepositoryTool(_repositoryRoot);

            var result = await tool.ExecuteAsync("needle");

            Assert.Equal(49, result.Matches.Count);
            Assert.False(result.IsTruncated);
        }

        [Fact]
        public async Task ExecuteAsync_ExactlyTheLimit_IsNotTruncated()
        {
            await WriteNeedleLines(50);

            var tool = new SearchRepositoryTool(_repositoryRoot);

            var result = await tool.ExecuteAsync("needle");

            Assert.Equal(50, result.Matches.Count);
            Assert.False(result.IsTruncated);
        }

        [Fact]
        public async Task ExecuteAsync_MoreMatchesThanLimit_ReturnsBoundedResultsAndIsTruncated()
        {
            await WriteNeedleLines(150);

            var tool = new SearchRepositoryTool(_repositoryRoot);

            var result = await tool.ExecuteAsync("needle");

            Assert.Equal(50, result.Matches.Count);
            Assert.True(result.IsTruncated);
        }

        private async Task WriteNeedleLines(int count)
        {
            var lines = Enumerable.Range(0, count)
                .Select(i => $"needle {i}")
                .ToArray();
            await File.WriteAllLinesAsync(
                Path.Combine(_repositoryRoot, "file.txt"),
                lines);
        }

        public void Dispose()
        {
            if (Directory.Exists(_repositoryRoot))
                Directory.Delete(_repositoryRoot, recursive: true);
        }
    }
}
