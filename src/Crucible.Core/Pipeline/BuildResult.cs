namespace Crucible.Core.Pipeline;

using System.Diagnostics;

#pragma warning disable CA1002 // Do not expose generic lists — BuildResult is a simple DTO for pipeline results

public sealed class BuildResult
{
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool Success => Errors.Count == 0;
    public Stopwatch? ParseTiming { get; set; }
    public Stopwatch? TransformTiming { get; set; }
}
