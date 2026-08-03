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
                var context = new PluginLoadContext(Path.GetFullPath(dll));
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

    /// <summary>
    /// Load context for one plugin, resolving that plugin's own dependencies from its
    /// published output while keeping the host's assemblies shared.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plugin is a published output folder, not a loose <c>.dll</c>: dependency resolution
    /// reads the <c>.deps.json</c> beside it. Without this, a plugin with any dependency
    /// loads successfully and then throws <see cref="FileNotFoundException"/> at the first
    /// call into that dependency, at a stack depth that hides the cause.
    /// </para>
    /// <para>
    /// The context is deliberately NOT collectible. Collectible contexts cost indirection on
    /// every call and reduced JIT optimization, and plugins are loaded once for the lifetime
    /// of the build. If a watch mode ever needs to reload a rebuilt plugin, make it
    /// collectible then — and actually call <c>Unload</c>.
    /// </para>
    /// </remarks>
    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// Assemblies that must come from the host even when the plugin ships its own copy.
        /// </summary>
        /// <remarks>
        /// A published plugin folder contains Crucible.Core.dll and everything Crucible.Core
        /// references. Loading those from the plugin folder produces types with the same
        /// names in a different context, which are not the same <see cref="Type"/> — so
        /// <c>typeof(ICrucibleExtension).IsAssignableFrom(t)</c> returns false and every
        /// plugin is silently skipped. The interface also mentions Markdig types, so the
        /// contract assembly alone is not enough.
        /// </remarks>
        private static readonly HashSet<string> HostOwned = BuildHostOwnedSet();

        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath)
            : base(Path.GetFileNameWithoutExtension(pluginPath), isCollectible: false)
            => _resolver = new AssemblyDependencyResolver(pluginPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Returning null defers to the default context.
            if (assemblyName.Name != null && HostOwned.Contains(assemblyName.Name))
                return null;

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path != null ? LoadFromAssemblyPath(path) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path != null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
        }

        private static HashSet<string> BuildHostOwnedSet()
        {
            var contract = typeof(ICrucibleExtension).Assembly;
            var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (contract.GetName().Name is { } contractName)
                owned.Add(contractName);

            // Direct references cover the types that appear in the extension contract —
            // Markdig most importantly. Framework assemblies need no entry: the resolver
            // returns null for them, which already defers to the default context.
            foreach (var referenced in contract.GetReferencedAssemblies())
            {
                if (referenced.Name is { } name)
                    owned.Add(name);
            }

            return owned;
        }
    }
}
