namespace Crucible.Cli;

using Crucible.Core.Models;
using Crucible.Core.Pipeline;
using Crucible.Extensions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

internal static class BuildCommand
{
    public static async Task<int> ExecuteAsync(string[] args)
    {
        var opts = ParseArgs(args);

        if (opts.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        // Load config from crucible.yaml if present
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "crucible.yaml");
        var config = new CrucibleConfig();

        if (File.Exists(configPath))
        {
            var yaml = await File.ReadAllTextAsync(configPath).ConfigureAwait(true);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            config = deserializer.Deserialize<CrucibleConfig>(yaml) ?? new CrucibleConfig();
        }

        // CLI flags override config file values
        if (opts.Source != null) config.Source = opts.Source;
        if (opts.Output != null) config.Output = opts.Output;
        if (opts.Theme != null) config.Theme = opts.Theme;
        if (opts.BaseUrl != null) config.BaseUrl = opts.BaseUrl;
        if (opts.Title != null) config.Title = opts.Title;

        // Resolve relative paths
        config.Source = Path.GetFullPath(config.Source);
        config.Output = Path.GetFullPath(config.Output);

        if (!Directory.Exists(config.Source))
        {
            await Console.Error.WriteLineAsync(
                $"Source directory not found: {config.Source}").ConfigureAwait(true);
            return 1;
        }

        // Register extensions
        var extensions = ExtensionRegistry.DefaultExtensions;

        var buildOptions = new BuildOptions
        {
            Stage = opts.Stage,
            Clean = opts.Clean,
            IncludeDrafts = opts.IncludeDrafts,
            Strict = opts.Strict,
            Verbose = opts.Verbose,
            Timing = opts.Timing,
        };

        var pipeline = new BuildPipeline(config, extensions, buildOptions);
        var result = await pipeline.ExecuteAsync().ConfigureAwait(true);

        // Print warnings to stderr
        foreach (var warning in result.Warnings)
        {
            await Console.Error.WriteLineAsync($"warning: {warning}").ConfigureAwait(true);
        }

        // Print errors to stderr
        foreach (var error in result.Errors)
        {
            await Console.Error.WriteLineAsync($"error: {error}").ConfigureAwait(true);
        }

        // Print timing if requested
        if (opts.Timing)
        {
            if (result.ParseTiming != null)
            {
                await Console.Error.WriteLineAsync(
                    $"Parse: {result.ParseTiming.ElapsedMilliseconds}ms").ConfigureAwait(true);
            }

            if (result.TransformTiming != null)
            {
                await Console.Error.WriteLineAsync(
                    $"Transform: {result.TransformTiming.ElapsedMilliseconds}ms").ConfigureAwait(true);
            }
        }

        if (!result.Success)
        {
            // Determine exit code based on which stage failed
            return result.TransformTiming != null ? 3 : 2;
        }

        return 0;
    }

    private static CliOptions ParseArgs(string[] args)
    {
        var opts = new CliOptions();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    opts.ShowHelp = true;
                    break;
                case "--source" or "-s" when i + 1 < args.Length:
                    opts.Source = args[++i];
                    break;
                case "--output" or "-o" when i + 1 < args.Length:
                    opts.Output = args[++i];
                    break;
                case "--theme" or "-t" when i + 1 < args.Length:
                    opts.Theme = args[++i];
                    break;
                case "--base-url" when i + 1 < args.Length:
                    opts.BaseUrl = args[++i];
                    break;
                case "--title" when i + 1 < args.Length:
                    opts.Title = args[++i];
                    break;
                case "--stage" when i + 1 < args.Length:
                    opts.Stage = Enum.Parse<BuildStage>(args[++i], ignoreCase: true);
                    break;
                case "--verbose" or "-v":
                    opts.Verbose = true;
                    break;
                case "--timing":
                    opts.Timing = true;
                    break;
                case "--clean":
                    opts.Clean = true;
                    break;
                case "--include-drafts":
                    opts.IncludeDrafts = true;
                    break;
                case "--strict":
                    opts.Strict = true;
                    break;
            }
        }

        return opts;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Usage: crucible [build] [options]
                   crucible init [--force]
                   crucible --version

            Build options:
              -s, --source <dir>     Source directory (default: ./docs)
              -o, --output <dir>     Output directory (default: ./dist)
              -t, --theme <dir>      Theme directory
              --base-url <url>       Base URL prefix (default: /)
              --title <title>        Site title
              --stage <stage>        Build stage: Full, ParseOnly, TransformOnly
              --clean                Delete output directory before building
              --include-drafts       Include draft documents
              --strict               Treat warnings as errors
              --verbose              Verbose output
              --timing               Print stage timing
              -h, --help             Show this help
            """);
    }

    private sealed class CliOptions
    {
        public string? Source { get; set; }
        public string? Output { get; set; }
        public string? Theme { get; set; }
        public string? BaseUrl { get; set; }
        public string? Title { get; set; }
        public BuildStage Stage { get; set; } = BuildStage.Full;
        public bool Clean { get; set; }
        public bool IncludeDrafts { get; set; }
        public bool Strict { get; set; }
        public bool Verbose { get; set; }
        public bool Timing { get; set; }
        public bool ShowHelp { get; set; }
    }
}
