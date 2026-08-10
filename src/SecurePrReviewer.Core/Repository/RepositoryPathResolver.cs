namespace SecurePrReviewer.Core.Repository;

/// <summary>Resolves paths against a repository root, rejecting any path that would escape it.</summary>
public sealed class RepositoryPathResolver
{
    private readonly string _repositoryRoot;

    /// <summary>Creates a resolver bounded to the given repository root.</summary>
    /// <param name="repositoryRoot">Directory all resolved paths must stay within.</param>
    public RepositoryPathResolver(string repositoryRoot)
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
    }

    /// <summary>Resolves <paramref name="relativePath"/> to a full path within the repository root.</summary>
    /// <param name="relativePath">Path to resolve, relative to the repository root.</param>
    /// <returns>The resolved absolute path.</returns>
    /// <exception cref="ArgumentException"><paramref name="relativePath"/> is empty or whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">The resolved path falls outside the repository root.</exception>
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