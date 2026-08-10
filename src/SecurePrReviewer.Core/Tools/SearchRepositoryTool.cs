namespace SecurePrReviewer.Core.Tools;

/// <summary>A single search hit: the file it was found in, its line number, and the line's text.</summary>
/// <param name="RelativePath">Path of the matching file, relative to the repository root.</param>
/// <param name="LineNumber">1-based line number of the match.</param>
/// <param name="LineText">The full text of the matching line.</param>
public sealed record SearchMatch(string RelativePath, int LineNumber, string LineText);

/// <summary>Result of a repository search: the matches found, and whether more existed than were returned.</summary>
/// <param name="Matches">The matches found, up to the tool's result limit.</param>
/// <param name="IsTruncated">True if additional matches existed beyond the result limit.</param>
public sealed record SearchResult(IReadOnlyList<SearchMatch> Matches, bool IsTruncated);

/// <summary>Searches text files within a repository for a literal string, bounded to the repository root.</summary>
public sealed class SearchRepositoryTool
{
    private const int MaxResults = 50;

    private static readonly string[] IgnoredDirectories = { ".git", "bin", "obj" };

    private readonly string _repositoryRoot;

    /// <summary>Creates a tool scoped to the given repository root.</summary>
    /// <param name="repositoryRoot">Directory to search; all results are bounded to this root.</param>
    public SearchRepositoryTool(string repositoryRoot)
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
    }

    /// <summary>Recursively searches repository files for lines containing <paramref name="query"/>.</summary>
    /// <param name="query">Literal text to search for; must not be empty or whitespace.</param>
    /// <param name="cancellationToken">Token used to cancel the search.</param>
    /// <returns>Up to 50 matches, in file-enumeration order, with <see cref="SearchResult.IsTruncated"/> set if more existed.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is empty or whitespace.</exception>
    public async Task<SearchResult> ExecuteAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Search query is required.", nameof(query));

        var matches = new List<SearchMatch>();

        foreach (var filePath in EnumerateSearchableFiles(_repositoryRoot))
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(query, StringComparison.Ordinal))
                {
                    if (matches.Count == MaxResults)
                        return new SearchResult(matches, IsTruncated: true);

                    matches.Add(new SearchMatch(
                        Path.GetRelativePath(_repositoryRoot, filePath),
                        i + 1,
                        lines[i]));
                }
            }
        }

        return new SearchResult(matches, IsTruncated: false);
    }

    private static IEnumerable<string> EnumerateSearchableFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
            yield return file;

        foreach (var subDirectory in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subDirectory);
            if (IgnoredDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;

            foreach (var file in EnumerateSearchableFiles(subDirectory))
                yield return file;
        }
    }
}
