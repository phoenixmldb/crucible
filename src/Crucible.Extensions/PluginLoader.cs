namespace Crucible.Extensions;

using System.Reflection;
using System.Runtime.Loader;
using Crucible.Core.Extensions;

#pragma warning disable CA1002 // Do not expose generic lists — simple factory method for plugin loading

public static class PluginLoader
{
    public static List<ICrucibleExtension> LoadPlugins(string? pluginsDir,
        IEnumerable<string>? configExtensions = null)
    {
        var extensions = new List<ICrucibleExtension>();
        var assemblyPaths = new List<string>();

        if (pluginsDir != null && Directory.Exists(pluginsDir))
            assemblyPaths.AddRange(Directory.EnumerateFiles(pluginsDir, "*.dll"));

        if (configExtensions != null)
        {
            foreach (var name in configExtensions)
            {
                var dllPath = pluginsDir != null ? Path.Combine(pluginsDir, $"{name}.dll") : null;
                if (dllPath != null && File.Exists(dllPath) && !assemblyPaths.Contains(dllPath))
                    assemblyPaths.Add(dllPath);
            }
        }

        foreach (var dll in assemblyPaths)
        {
            try
            {
                var context = new AssemblyLoadContext(
                    Path.GetFileNameWithoutExtension(dll), isCollectible: true);
                var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dll));
                foreach (var type in assembly.GetExportedTypes()
                    .Where(t => typeof(ICrucibleExtension).IsAssignableFrom(t)
                        && !t.IsAbstract && !t.IsInterface))
                {
                    if (Activator.CreateInstance(type) is ICrucibleExtension ext)
                        extensions.Add(ext);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Warning: Failed to load plugin {dll}: {ex.Message}");
            }
        }

        return extensions;
    }
}
