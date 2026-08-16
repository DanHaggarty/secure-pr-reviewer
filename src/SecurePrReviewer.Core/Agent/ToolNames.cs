namespace SecurePrReviewer.Core.Agent;

/// <summary>Canonical names for the tools offered to the model, shared between <see cref="SecurityReviewAgent"/> and <see cref="ToolPolicy"/>.</summary>
internal static class ToolNames
{
    /// <summary>Reads a file's contents from the repository.</summary>
    public const string ReadFile = "read_file";

    /// <summary>Searches text files in the repository for a literal string.</summary>
    public const string SearchRepository = "search_repository";

    /// <summary>Submits the completed security review, ending the loop.</summary>
    public const string SubmitFindings = "submit_findings";
}
