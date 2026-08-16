using System.Text.Json;
using SecurePrReviewer.Core.Llm;
using SecurePrReviewer.Core.Tools;

namespace SecurePrReviewer.Core.Agent;

/// <summary>Authorizes and executes a model-requested tool call, never throwing.</summary>
public sealed class ToolPolicy
{
    private readonly ReadFileTool _readFileTool;
    private readonly SearchRepositoryTool _searchRepositoryTool;

    /// <summary>Creates a policy that executes tool calls against the given tools.</summary>
    /// <param name="readFileTool">Executes "read_file" tool calls.</param>
    /// <param name="searchRepositoryTool">Executes "search_repository" tool calls.</param>
    public ToolPolicy(ReadFileTool readFileTool, SearchRepositoryTool searchRepositoryTool)
    {
        _readFileTool = readFileTool;
        _searchRepositoryTool = searchRepositoryTool;
    }

    /// <summary>Authorizes and executes <paramref name="toolCall"/>, returning a safe observation.</summary>
    /// <param name="toolCall">The tool call the model requested.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The tool's result, or a rejection/error message — never throws.</returns>
    public async Task<string> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
    {
        try
        {
            return toolCall.Name switch
            {
                ToolNames.ReadFile => await _readFileTool.ExecuteAsync(
                    GetArgument(toolCall.ArgumentsJson, "path"), cancellationToken),
                ToolNames.SearchRepository => FormatSearchResult(
                    await _searchRepositoryTool.ExecuteAsync(
                        GetArgument(toolCall.ArgumentsJson, "query"), cancellationToken)),
                _ => $"Error: tool '{toolCall.Name}' is not permitted."
            };
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string GetArgument(string argumentsJson, string propertyName)
    {
        using var document = JsonDocument.Parse(argumentsJson);
        return document.RootElement.GetProperty(propertyName).GetString()
            ?? throw new ArgumentException($"Argument '{propertyName}' was null.");
    }

    private static string FormatSearchResult(SearchResult result)
    {
        if (result.Matches.Count == 0)
            return "No matches found.";

        var lines = result.Matches.Select(m => $"{m.RelativePath}:{m.LineNumber}: {m.LineText}");
        var text = string.Join(Environment.NewLine, lines);

        return result.IsTruncated
            ? $"{text}{Environment.NewLine}(results truncated — refine your search query or read a specific file if you need more)"
            : text;
    }
}
