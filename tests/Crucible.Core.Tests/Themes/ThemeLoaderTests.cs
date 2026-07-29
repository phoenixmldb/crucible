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

    [Theory]
    [InlineData("../../../../etc")]
    [InlineData("default/../../..")]
    [InlineData("_base")]
    [InlineData("default/css")]
    public void Ctor_NameEscapingThemesDirectory_IsNotResolvedAsBuiltIn(string name)
    {
        // Built-in lookup takes a bare directory name under Themes/. A name
        // carrying separators or traversal segments must never resolve there,
        // and _base is a shared stylesheet fragment, not a theme.
        var act = () => new ThemeLoader(name);

        act.Should().Throw<IOException>()
            .Which.Should().Match(e =>
                e is DirectoryNotFoundException || e is FileNotFoundException);
    }

    [Fact]
    public void Ctor_DirectoryThatIsNotATheme_NamesTheMissingFile()
    {
        var notATheme = Path.Combine(AppContext.BaseDirectory, "Themes");

        var act = () => new ThemeLoader(notATheme);

        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*page.xslt*");
    }

    [Fact]
    public void Ctor_ExplicitRelativeDirectory_StillWorks()
    {
        // Path traversal is only blocked for built-in *name* lookup. An explicit
        // directory the user points at is theirs to choose.
        var builtInDefault = Path.Combine(AppContext.BaseDirectory, "Themes", "default");
        var viaRelative = Path.Combine(builtInDefault, "..", "default");

        var loader = new ThemeLoader(viaRelative);
        loader.PageXslt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetStaticAssets_DoesNotLeakSharedBaseStylesheets()
    {
        var loader = new ThemeLoader("modern");
        var assets = loader.GetStaticAssets().Select(a => a.RelativePath).ToList();

        assets.Should().NotBeEmpty();
        assets.Should().AllSatisfy(p => p.Should().NotContain("_base"));
        assets.Should().AllSatisfy(p => p.Should().NotEndWith(".xslt"));
    }
}
