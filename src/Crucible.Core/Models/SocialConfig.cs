namespace Crucible.Core.Models;

#pragma warning disable CA1056 // URI properties should not be strings — values are paths relative to base-url, or absolute URLs

/// <summary>
/// Link-preview settings for social sites (X/Twitter, LinkedIn, Slack, Mastodon). Optional:
/// without it, pages carry a text-only <c>summary</c> card.
/// <code>
/// social:
///   image: img/icon-256.png         # relative to base-url, or an absolute URL
///   card: summary                   # summary (square icon, the default) or summary_large_image (1200x630 banner)
///   image-alt: Product name and tagline
///   twitter-site: "@handle"         # optional
/// </code>
/// </summary>
public sealed class SocialConfig
{
    /// <summary>
    /// The preview image. A relative path is joined to <c>base-url</c>, so <c>base-url</c> must be
    /// an absolute URL for crawlers to fetch it.
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// The X/Twitter card type: <c>summary</c> (a small square image, at least 144x144; the
    /// default) or <c>summary_large_image</c> (a wide banner, ideally 1200x630).
    /// </summary>
    public string? Card { get; set; }

    /// <summary>Alt text for the preview image.</summary>
    [YamlDotNet.Serialization.YamlMember(Alias = "image-alt")]
    public string? ImageAlt { get; set; }

    /// <summary>The site's X/Twitter account, with the <c>@</c>.</summary>
    [YamlDotNet.Serialization.YamlMember(Alias = "twitter-site")]
    public string? TwitterSite { get; set; }
}
