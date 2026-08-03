namespace Crucible.Extensions.Tests;

using Crucible.Extensions;
using FluentAssertions;
using Xunit;

/// <summary>
/// The plugin loader had no tests, because the only extension that ever exercised it is
/// in-repo and dependency-free. A real plugin is a published output folder: it brings its
/// own dependencies, and it brings its own copy of the contract assembly.
/// </summary>
public sealed class PluginLoaderTests
{
    private static string PluginDirectory =>
        Path.Combine(AppContext.BaseDirectory, "test-plugin");

    [Fact]
    public void PluginDirectory_IsStaged()
    {
        // Guards the build wiring. Without this, every test below would pass vacuously by
        // finding nothing to load.
        Directory.Exists(PluginDirectory).Should().BeTrue();
        Directory.GetFiles(PluginDirectory, "SamplePlugin.dll").Should().NotBeEmpty();
        Directory.GetFiles(PluginDirectory, "SamplePlugin.Support.dll").Should().NotBeEmpty(
            "the dependency must ship in the plugin folder for its resolution to mean anything");
    }

    [Fact]
    public void LoadPlugins_DiscoversAnExtensionInAPublishedPluginFolder()
    {
        var extensions = PluginLoader.LoadPlugins(PluginDirectory);

        extensions.Should().ContainSingle(
            "the plugin folder contains its own Crucible.Core.dll — resolving the contract " +
            "from there instead of the default context yields a different Type and skips the plugin");
    }

    [Fact]
    public void LoadedPlugin_CanCallItsOwnDependency()
    {
        var extensions = PluginLoader.LoadPlugins(PluginDirectory);

        // The failure this reproduces: the plugin loads, then throws FileNotFoundException
        // at the first call into a dependency the loader never resolved.
        var name = extensions.Single().Name;

        name.Should().Be("sample-plugin (support loaded)");
    }
}
