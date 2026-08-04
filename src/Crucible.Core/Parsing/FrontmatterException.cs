namespace Crucible.Core.Parsing;

/// <summary>
/// Frontmatter that could not be parsed, attributed to the document it came from.
/// </summary>
/// <remarks>
/// The underlying YAML error reports a line number relative to the frontmatter block, which
/// is not a location anyone can act on: a single bad file fails the whole build with a
/// message that does not name the file or point into it. <see cref="Line"/> is translated to
/// be relative to the document.
/// </remarks>
public sealed class FrontmatterException : Exception
{
    /// <summary>Document the frontmatter came from, as supplied by the caller.</summary>
    public string DocumentPath { get; } = "";

    /// <summary>1-based line number within the document, not within the block.</summary>
    public int Line { get; }

    public FrontmatterException() { }

    public FrontmatterException(string message) : base(message) { }

    public FrontmatterException(string message, Exception innerException)
        : base(message, innerException) { }

    public FrontmatterException(string documentPath, int line, string reason, Exception innerException)
        : base($"Invalid frontmatter in {documentPath} (line {line}): {reason}", innerException)
    {
        DocumentPath = documentPath;
        Line = line;
    }
}
