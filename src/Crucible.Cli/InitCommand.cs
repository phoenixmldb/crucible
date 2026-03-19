namespace Crucible.Cli;

internal static class InitCommand
{
    public static async Task<int> ExecuteAsync(bool force)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "crucible.yaml");

        if (File.Exists(configPath) && !force)
        {
            await Console.Error.WriteLineAsync(
                "crucible.yaml already exists. Use --force to overwrite.").ConfigureAwait(true);
            return 1;
        }

        await File.WriteAllTextAsync(configPath, """
            # Crucible documentation site configuration
            title: My Documentation
            base-url: /
            source: ./docs
            output: ./dist
            # theme: ./my-theme   # Uncomment to use a custom theme
            # extensions:
            #   - Crucible.Extensions.Mermaid
            """).ConfigureAwait(true);

        var docsDir = Path.Combine(Directory.GetCurrentDirectory(), "docs");
        var createdDocs = !Directory.Exists(docsDir);
        if (createdDocs)
        {
            Directory.CreateDirectory(docsDir);
            await File.WriteAllTextAsync(Path.Combine(docsDir, "index.md"), """
                ---
                title: Welcome
                description: Welcome to your documentation site
                sort: 0
                ---

                # Welcome

                This is your documentation site, powered by [Crucible](https://github.com/phoenixmldb/crucible).

                ## Getting Started

                Edit this file or add new `.md` files to the `docs/` directory.

                Run `crucible build` to generate your static site.
                """).ConfigureAwait(true);
        }

        Console.WriteLine("Created crucible.yaml");
        if (createdDocs)
            Console.WriteLine("Created docs/index.md");

        return 0;
    }
}
