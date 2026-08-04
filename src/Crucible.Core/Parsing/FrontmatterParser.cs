namespace Crucible.Core.Parsing;

using Crucible.Core.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class FrontmatterParser
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <param name="content">Full document text.</param>
    /// <param name="documentPath">
    /// Document identity used to attribute parse failures. Malformed frontmatter fails the
    /// build, so an error that does not name the file is not actionable.
    /// </param>
    /// <exception cref="FrontmatterException">The frontmatter block is not valid YAML.</exception>
    public static (DocumentMetadata? Metadata, string Markdown) Parse(
        string content, string documentPath)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.StartsWith("---", StringComparison.Ordinal))
            return (null, content);

        if (!TryFindTerminator(content, out var blockEnd, out var bodyStart))
            return (null, content);

        var yamlBlock = content[3..blockEnd].Trim();
        var markdown = content[bodyStart..].TrimStart('\r', '\n');

        try
        {
            var metadata = YamlDeserializer.Deserialize<DocumentMetadata>(yamlBlock);
            return (metadata, markdown);
        }
        catch (YamlException ex)
        {
            // YamlDotNet numbers lines within the block it was handed. The block starts
            // after the opening --- delimiter, and Trim() removes the newline that followed
            // it, so block line N is document line N + 1.
            throw new FrontmatterException(
                documentPath, (int)ex.Start.Line + 1, ex.Message, ex);
        }
    }

    /// <summary>
    /// Locates the closing delimiter.
    /// </summary>
    /// <param name="content">Full document text.</param>
    /// <param name="blockEnd">Index of the newline that ends the YAML block.</param>
    /// <param name="bodyStart">Index of the first character after the delimiter line.</param>
    /// <remarks>
    /// The delimiter must be a line that is exactly <c>---</c>, ignoring trailing
    /// whitespace. Accepting any line that merely starts with it lets a <c>----</c>
    /// horizontal rule, or a typo, end the block early — silently truncating the metadata
    /// and leaking the remainder into the rendered page.
    /// <para>
    /// <paramref name="bodyStart"/> is reported rather than derived from
    /// <paramref name="blockEnd"/>, because the delimiter line is not a fixed width once
    /// trailing whitespace is allowed.
    /// </para>
    /// </remarks>
    private static bool TryFindTerminator(string content, out int blockEnd, out int bodyStart)
    {
        var searchFrom = 3;

        while (true)
        {
            var candidate = content.IndexOf("\n---", searchFrom, StringComparison.Ordinal);
            if (candidate < 0)
            {
                blockEnd = -1;
                bodyStart = -1;
                return false;
            }

            var lineEnd = content.IndexOf('\n', candidate + 1);
            var line = lineEnd < 0 ? content[(candidate + 1)..] : content[(candidate + 1)..lineEnd];

            // TrimEnd covers the \r of CRLF as well as stray trailing spaces.
            if (line.TrimEnd() == "---")
            {
                blockEnd = candidate;
                bodyStart = lineEnd < 0 ? content.Length : lineEnd;
                return true;
            }

            searchFrom = candidate + 1;
        }
    }
}
