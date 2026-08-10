using SecurePrReviewer.Core.Repository;
using SecurePrReviewer.Core.Tools;

namespace SecurePrReviewer.Tests
{
    public class ReadFileToolTests : IDisposable
    {
        private readonly string _repositoryRoot;
        private readonly ReadFileTool _tool;

        public ReadFileToolTests()
        {
            _repositoryRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_repositoryRoot);
            _tool = new ReadFileTool(new RepositoryPathResolver(_repositoryRoot));
        }

        [Fact]
        public async Task ExecuteAsync_ExistingFile_ReturnsContents()
        {
            var filePath = Path.Combine(_repositoryRoot, "file.txt");
            await File.WriteAllTextAsync(filePath, "hello world");

            var contents = await _tool.ExecuteAsync("file.txt");

            Assert.Equal("hello world", contents);
        }

        [Fact]
        public async Task ExecuteAsync_NestedFile_ReturnsContents()
        {
            var nestedDir = Path.Combine(_repositoryRoot, "nested");
            Directory.CreateDirectory(nestedDir);
            await File.WriteAllTextAsync(Path.Combine(nestedDir, "file.txt"), "nested contents");

            var contents = await _tool.ExecuteAsync(Path.Combine("nested", "file.txt"));

            Assert.Equal("nested contents", contents);
        }

        [Fact]
        public async Task ExecuteAsync_MissingFile_ThrowsFileNotFoundException()
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                _tool.ExecuteAsync("missing.txt"));
        }

        [Fact]
        public async Task ExecuteAsync_PathTraversal_ThrowsUnauthorizedAccessException()
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _tool.ExecuteAsync("../secrets.txt"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_repositoryRoot))
                Directory.Delete(_repositoryRoot, recursive: true);
        }
    }
}
