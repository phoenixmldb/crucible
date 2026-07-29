using System.Reflection;
using Crucible.Cli;

if (args.Length > 0 && args[0] is "init")
{
    var force = args.Contains("--force") || args.Contains("-f");
    return await InitCommand.ExecuteAsync(force).ConfigureAwait(true);
}

if (args.Length > 0 && args[0] is "--version")
{
    // Read the real assembly version rather than a literal. The literal was
    // never updated, so every published build from 1.0.0 through 1.1.47
    // reported "1.0.0-preview.1" — you could not tell which tool you were
    // running, which makes any smoke test against a published version
    // unfalsifiable.
    var informational = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion;

    // SourceLink appends "+<commit sha>"; keep the version, drop the metadata.
    var version = informational is null
        ? "unknown"
        : informational.Split('+')[0];

    Console.WriteLine($"crucible {version} (Crucible Documentation Generator)");
    return 0;
}

return await BuildCommand.ExecuteAsync(args).ConfigureAwait(true);
