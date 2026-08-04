namespace Crucible.Core.Pipeline;

using System.Diagnostics;
using Crucible.Core.Extensions;
using Crucible.Core.Models;

#pragma warning disable CA1002 // Do not expose generic lists — BuildOptions is a simple DTO

public sealed class BuildPipeline
{
    private readonly CrucibleConfig _config;
    private readonly List<ICrucibleExtension> _extensions;
    private readonly BuildOptions _options;

    public BuildPipeline(CrucibleConfig config,
        IEnumerable<ICrucibleExtension> extensions, BuildOptions options)
    {
        _config = config;
        _extensions = extensions.ToList();
        _options = options;
    }

    public async Task<BuildResult> ExecuteAsync(CancellationToken ct = default)
    {
        var result = new BuildResult();
        var inputType = InputDetector.Detect(_config.Source);

        if (_options.Verbose)
        {
            result.Messages.Add($"Input: {_config.Source} (detected as {inputType})");
            result.Messages.Add($"Output: {_config.Output}");
        }

        if (_options.Clean && Directory.Exists(_config.Output))
            Directory.Delete(_config.Output, recursive: true);

        if (inputType == InputType.MarkdownSource &&
            _options.Stage != BuildStage.TransformOnly)
        {
            var parseSw = Stopwatch.StartNew();
            var parseOutput = _options.Stage == BuildStage.ParseOnly
                ? _config.Output
                : Path.Combine(Path.GetTempPath(), $"crucible-{Guid.NewGuid()}");

            // A full build parses into a GUID-named temp directory and deletes it on the
            // way out, so there is otherwise no way to inspect the intermediate XML that
            // produced a wrong-looking page. Under --verbose the directory is reported and
            // kept — naming a path that was then deleted would not be diagnostics.
            if (_options.Verbose)
                result.Messages.Add($"Intermediate: {parseOutput} (kept for inspection)");

            var parseResult = await ParseStage.ExecuteAsync(
                _config.Source, parseOutput,
                _config.Title, _config.BaseUrl,
                _extensions, _options.IncludeDrafts, ct).ConfigureAwait(false);

            result.Errors.AddRange(parseResult.Errors);
            result.Warnings.AddRange(parseResult.Warnings);
            result.Messages.AddRange(parseResult.Messages);
            parseSw.Stop();
            result.ParseTiming = parseSw;

            if (!result.Success || _options.Stage == BuildStage.ParseOnly)
                return Finish(result);

            var transformSw = Stopwatch.StartNew();
            var transformResult = await TransformStage.ExecuteAsync(
                parseOutput, _config.Output, _config.Theme, _extensions,
                _config.Analytics, ct).ConfigureAwait(false);
            result.Errors.AddRange(transformResult.Errors);
            result.Warnings.AddRange(transformResult.Warnings);
            result.Messages.AddRange(transformResult.Messages);
            transformSw.Stop();
            result.TransformTiming = transformSw;

            // Clean up temp intermediate dir, unless --verbose promised to keep it.
            if (!_options.Verbose)
            {
                try { Directory.Delete(parseOutput, recursive: true); }
                catch (IOException) { /* best effort */ }
            }
        }
        else if (inputType == InputType.XmlIntermediate)
        {
            var transformSw = Stopwatch.StartNew();
            var transformResult = await TransformStage.ExecuteAsync(
                _config.Source, _config.Output, _config.Theme, _extensions,
                _config.Analytics, ct).ConfigureAwait(false);
            result.Errors.AddRange(transformResult.Errors);
            result.Warnings.AddRange(transformResult.Warnings);
            result.Messages.AddRange(transformResult.Messages);
            transformSw.Stop();
            result.TransformTiming = transformSw;
        }

        return Finish(result);

        // --strict is advertised as "treat warnings as errors". The flag was previously
        // parsed, stored on BuildOptions and never read, so a CI pipeline passing it got a
        // green build no matter what was reported. The warnings stay in the collection so
        // they still print; the added error is what makes Success false.
        BuildResult Finish(BuildResult r)
        {
            if (_options.Strict && r.Warnings.Count > 0)
            {
                r.Errors.Add(
                    $"{r.Warnings.Count} warning(s) treated as errors because --strict was specified.");
            }

            return r;
        }
    }
}

public sealed class BuildOptions
{
    public BuildStage Stage { get; init; } = BuildStage.Full;
    public bool Clean { get; init; }
    public bool IncludeDrafts { get; init; }
    public bool Strict { get; init; }
    public bool Verbose { get; init; }
    public bool Timing { get; init; }
}

public enum BuildStage
{
    Full,
    ParseOnly,
    TransformOnly,
}
