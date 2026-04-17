namespace Crucible.Core.Tests.Themes;

using Crucible.Core.Themes;
using FluentAssertions;
using Xunit;

public class ThemeLoaderTests
{
    [Fact]
    public void Ctor_NullPath_ResolvesDefaultBuiltIn()
    {
        var loader = new ThemeLoader(null);
        loader.ThemeDirectory.Should()
            .EndWith(Path.Combine("Themes", "default"));
        loader.PageXslt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Ctor_BuiltInName_ResolvesAgainstAppContext()
    {
        var loader = new ThemeLoader("default");
        loader.ThemeDirectory.Should()
            .EndWith(Path.Combine("Themes", "default"));
        loader.PageXslt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Ctor_AbsoluteDirectory_UsesDirectAsIs()
    {
        var builtInDefault = Path.Combine(AppContext.BaseDirectory, "Themes", "default");
        var loader = new ThemeLoader(builtInDefault);
        loader.ThemeDirectory.Should().Be(builtInDefault);
    }

    [Fact]
    public void Ctor_UnknownBuiltInName_Throws()
    {
        var act = () => new ThemeLoader("does-not-exist");
        act.Should().Throw<DirectoryNotFoundException>();
    }
}
