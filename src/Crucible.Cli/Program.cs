using Crucible.Cli;

if (args.Length > 0 && args[0] is "init")
{
    var force = args.Contains("--force") || args.Contains("-f");
    return await InitCommand.ExecuteAsync(force).ConfigureAwait(true);
}

if (args.Length > 0 && args[0] is "--version")
{
    Console.WriteLine("crucible 1.0.0-preview.1 (Crucible Documentation Generator)");
    return 0;
}

return await BuildCommand.ExecuteAsync(args).ConfigureAwait(true);
