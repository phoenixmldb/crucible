namespace Crucible.Core.Parsing;

using System.Text.RegularExpressions;

public static partial class SlugGenerator
{
    public static string Generate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

#pragma warning disable CA1308 // Normalize strings to uppercase — slugs are lowercase by convention
        var slug = text.ToLowerInvariant();
#pragma warning restore CA1308
        slug = NonAlphanumericRegex().Replace(slug, "-");
        slug = MultipleHyphensRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleHyphensRegex();
}
