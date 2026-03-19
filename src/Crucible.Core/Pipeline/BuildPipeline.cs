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

        if (_options.Clean && Directory.Exists(_config.Output))
            Directory.Delete(_config.Output, recursive: true);

        if (inputType == InputType.MarkdownSource &&
            _options.Stage != BuildStage.TransformOnly)
        {
            var parseSw = Stopwatch.StartNew();
            var parseOutput = _options.Stage == BuildStage.ParseOnly
                ? _config.Output
                : Path.Combine(Path.GetTempPath(), $"crucible-{Guid.NewGuid()}");

            var parseResult = await ParseStage.ExecuteAsync(
                _config.Source, parseOutput,
                _config.Title, _config.BaseUrl,
                _extensions, _options.IncludeDrafts, ct).ConfigureAwait(true);

            result.Errors.AddRange(parseResult.Errors);
            result.Warnings.AddRange(parseResult.Warnings);
            parseSw.Stop();
            result.ParseTiming = parseSw;

            if (!result.Success || _options.Stage == BuildStage.ParseOnly)
                return result;

            var transformSw = Stopwatch.StartNew();
            var transformResult = await TransformStage.ExecuteAsync(
                parseOutput, _config.Output, _config.Theme, _extensions, ct).ConfigureAwait(true);
            result.Errors.AddRange(transformResult.Errors);
            result.Warnings.AddRange(transformResult.Warnings);
            transformSw.Stop();
            result.TransformTiming = transformSw;

            // Clean up temp intermediate dir
            try { Directory.Delete(parseOutput, recursive: true); }
            catch (IOException) { /* best effort */ }
        }
        else if (inputType == InputType.XmlIntermediate)
        {
            var transformSw = Stopwatch.StartNew();
            var transformResult = await TransformStage.ExecuteAsync(
                _config.Source, _config.Output, _config.Theme, _extensions, ct).ConfigureAwait(true);
            result.Errors.AddRange(transformResult.Errors);
            result.Warnings.AddRange(transformResult.Warnings);
            transformSw.Stop();
            result.TransformTiming = transformSw;
        }

        return result;
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
