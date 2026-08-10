using SecurePrReviewer.Core.Repository;

namespace SecurePrReviewer.Core.Tools;

public sealed class ReadFileTool
{
    private readonly RepositoryPathResolver _pathResolver;

    public ReadFileTool(RepositoryPathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

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