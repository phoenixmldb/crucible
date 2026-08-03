namespace Crucible.Core.Pipeline;

using System.Diagnostics;

#pragma warning disable CA1002 // Do not expose generic lists — BuildResult is a simple DTO for pipeline results

public sealed class BuildResult
{
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Something wrong with the source that the build worked around. Escalated to an error
    /// by <c>--strict</c>.
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// Something the build did that the author asked for and may want to confirm — a
    /// skipped draft, say. Reported, never escalated: <c>--strict</c> exists to catch
    /// defects, and failing a build over a work-in-progress page would make it unusable on
    /// any site that has one.
    /// </summary>
    public List<string> Messages { get; } = [];

    public bool Success => Errors.Count == 0;
    public Stopwatch? ParseTiming { get; set; }
    public Stopwatch? TransformTiming { get; set; }
}
