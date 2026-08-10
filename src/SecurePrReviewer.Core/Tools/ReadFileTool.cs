using SecurePrReviewer.Core.Repository;

namespace SecurePrReviewer.Core.Tools;

/// <summary>Reads a single file's contents, bounded to a repository root via <see cref="RepositoryPathResolver"/>.</summary>
public sealed class ReadFileTool
{
    private readonly RepositoryPathResolver _pathResolver;

    /// <summary>Creates a tool that resolves paths through the given <paramref name="pathResolver"/>.</summary>
    /// <param name="pathResolver">Resolver enforcing that read paths stay within the repository root.</param>
    public ReadFileTool(RepositoryPathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    /// <summary>Reads the full text of the file at <paramref name="path"/>.</summary>
    /// <param name="path">Path to the file, relative to the repository root.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The file's contents.</returns>
    /// <exception cref="UnauthorizedAccessException"><paramref name="path"/> resolves outside the repository root.</exception>
    /// <exception cref="FileNotFoundException">No file exists at the resolved path.</exception>
    public async Task<string> ExecuteAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var resolvedPath = _pathResolver.Resolve(path);

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException(
                "File does not exist.",
                path);

        return await File.ReadAllTextAsync(
            resolvedPath,
            cancellationToken);
    }
}