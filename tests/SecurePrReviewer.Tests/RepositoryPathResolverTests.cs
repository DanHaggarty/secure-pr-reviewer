using SecurePrReviewer.Core.Repository;

namespace SecurePrReviewer.Tests
{
    public class RepositoryPathResolverTests
    {
        private readonly string _repositoryRoot =
            Path.Combine(Path.GetTempPath(), "repo");

        [Fact]
        public void Resolve_PathTraversal_Throws()
        {
            var resolver = new RepositoryPathResolver(_repositoryRoot);

            Assert.Throws<UnauthorizedAccessException>(() =>
                resolver.Resolve("../../secrets.txt"));
        }

        [Fact]
        public void Resolve_AbsolutePathOutsideRoot_Throws()
        {
            var resolver = new RepositoryPathResolver(_repositoryRoot);
            var outsidePath = Path.Combine(Path.GetTempPath(), "other", "secrets.txt");

            Assert.Throws<UnauthorizedAccessException>(() =>
                resolver.Resolve(outsidePath));
        }

        [Fact]
        public void Resolve_RelativePathWithinRoot_ReturnsFullPath()
        {
            var resolver = new RepositoryPathResolver(_repositoryRoot);

            var resolved = resolver.Resolve(Path.Combine("src", "file.txt"));

            Assert.Equal(
                Path.GetFullPath(Path.Combine(_repositoryRoot, "src", "file.txt")),
                resolved);
        }

        [Fact]
        public void Resolve_TraversalThatStaysWithinRoot_DoesNotThrow()
        {
            var resolver = new RepositoryPathResolver(_repositoryRoot);

            var resolved = resolver.Resolve(Path.Combine("src", "..", "file.txt"));

            Assert.Equal(
                Path.GetFullPath(Path.Combine(_repositoryRoot, "file.txt")),
                resolved);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Resolve_EmptyOrWhitespacePath_Throws(string path)
        {
            var resolver = new RepositoryPathResolver(_repositoryRoot);

            Assert.Throws<ArgumentException>(() => resolver.Resolve(path));
        }
    }
}
