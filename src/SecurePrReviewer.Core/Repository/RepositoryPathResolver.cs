namespace SecurePrReviewer.Core.Repository;

public sealed class RepositoryPathResolver
{
    private readonly string _repositoryRoot;

    public RepositoryPathResolver(string repositoryRoot)
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
    }

    public string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Path is required.", nameof(relativePath));

        var resolvedPath = Path.GetFullPath(
            Path.Combine(_repositoryRoot, relativePath));

        var relative = Path.GetRelativePath(
            _repositoryRoot,
            resolvedPath);

        if (relative.StartsWith("..") ||
            Path.IsPathRooted(relative))
        {
            throw new UnauthorizedAccessException(
                "Path is outside the repository.");
        }

        return resolvedPath;
    }
}