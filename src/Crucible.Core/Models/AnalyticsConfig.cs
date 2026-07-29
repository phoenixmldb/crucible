namespace Crucible.Core.Models;

/// <summary>
/// Opt-in analytics for generated sites. Nothing is emitted unless the user
/// configures it in crucible.yaml:
/// <code>
/// analytics:
///   ga4: G-XXXXXXXXXX
/// </code>
/// </summary>
public sealed class AnalyticsConfig
{
    /// <summary>
    /// Google Analytics 4 measurement ID (the <c>G-</c> prefixed value).
    /// When null or empty, no tracking snippet is emitted.
    /// </summary>
    [YamlDotNet.Serialization.YamlMember(Alias = "ga4")]
    public string? Ga4 { get; set; }
}
