# Crucible Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Crucible, a static documentation generator that transforms Markdown → XML → XSLT 4.0 → HTML, demonstrating the PhoenixmlDb XSLT engine.

**Architecture:** Three-project layered architecture: Crucible.Core (pipeline engine, extension interfaces, theme support), Crucible.Extensions (built-in extensions like Mermaid, plugin discovery), Crucible.Cli (dotnet tool wrapper). Markdown parsed via Markdig, frontmatter via YamlDotNet, XML intermediate representation, XSLT transformation via PhoenixmlDb.Xslt.

**Tech Stack:** .NET 10, C# 14, Markdig, YamlDotNet, PhoenixmlDb.Xslt, xUnit v3, FluentAssertions

**Spec:** `docs/superpowers/specs/2026-03-19-crucible-design.md`

---

## File Structure

### Build Infrastructure (repo root)
- `Directory.Build.props` — shared MSBuild properties (net10.0, C# preview, warnings-as-errors)
- `Directory.Build.rsp` — `-nodeReuse:false`
- `Directory.Packages.props` — centralized NuGet versions
- `Crucible.slnx` — solution file
- `.gitignore` — standard .NET ignores
- `LICENSE` — license file

### Crucible.Core (`src/Crucible.Core/`)
- `Crucible.Core.csproj` — library project referencing Markdig, YamlDotNet, PhoenixmlDb.Xslt
- `Models/DocumentMetadata.cs` — frontmatter model (title, description, sort, tags, draft, template, updated)
- `Models/SiteManifest.cs` — site structure model (SiteManifest, SiteSection, SitePage)
- `Models/CrucibleConfig.cs` — configuration model deserialized from crucible.yaml
- `Extensions/ICrucibleExtension.cs` — extension interface, XmlEmitterContext, CrucibleAsset
- `Parsing/FrontmatterParser.cs` — splits YAML frontmatter from Markdown, deserializes to DocumentMetadata
- `Parsing/MarkdownToXmlEmitter.cs` — walks Markdig AST, emits Crucible XML schema, dispatches to extensions
- `Parsing/LinkResolver.cs` — resolves relative/root-relative .md links to .html, collects warnings
- `Parsing/SlugGenerator.cs` — generates URL-safe slugs from heading text for anchor IDs
- `Manifest/SiteManifestBuilder.cs` — scans directory structure + frontmatter, builds site manifest XML
- `Pipeline/ParseStage.cs` — orchestrates Stage 1 (discover files → parse → emit XML + manifest)
- `Pipeline/TransformStage.cs` — orchestrates Stage 2 (load XSLT → transform XML → emit HTML + sitemap)
- `Pipeline/BuildPipeline.cs` — full build orchestrator (runs stages, copies assets, reports errors)
- `Pipeline/BuildResult.cs` — result object with errors, warnings, timing
- `Pipeline/InputDetector.cs` — detects whether source dir contains Markdown or XML intermediate
- `Themes/ThemeLoader.cs` — loads theme XSLT and assets from theme directory

### Crucible.Extensions (`src/Crucible.Extensions/`)
- `Crucible.Extensions.csproj` — library project referencing Crucible.Core
- `Mermaid/MermaidExtension.cs` — ICrucibleExtension for Mermaid diagrams
- `ExtensionRegistry.cs` — registers built-in extensions, loads plugins from directory
- `PluginLoader.cs` — AssemblyLoadContext-based plugin loading

### Crucible.Cli (`src/Crucible.Cli/`)
- `Crucible.Cli.csproj` — exe project, PackAsTool=true, ToolCommandName=crucible
- `Program.cs` — entry point, command routing (init/build)
- `BuildCommand.cs` — handles `crucible build` with all flags
- `InitCommand.cs` — handles `crucible init`, scaffolds crucible.yaml + starter docs

### Default Theme (`src/Crucible.Core/Themes/default/`)
- `page.xslt` — main page transform (document XML → HTML5)
- `navigation.xslt` — sidebar and breadcrumbs from site manifest
- `sitemap.xslt` — sitemap.xml generation
- `css/style.css` — default responsive stylesheet
- `js/theme.js` — mobile nav toggle

### Tests
- `tests/Crucible.Core.Tests/Crucible.Core.Tests.csproj`
- `tests/Crucible.Core.Tests/Parsing/FrontmatterParserTests.cs`
- `tests/Crucible.Core.Tests/Parsing/MarkdownToXmlEmitterTests.cs`
- `tests/Crucible.Core.Tests/Parsing/LinkResolverTests.cs`
- `tests/Crucible.Core.Tests/Parsing/SlugGeneratorTests.cs`
- `tests/Crucible.Core.Tests/Manifest/SiteManifestBuilderTests.cs`
- `tests/Crucible.Core.Tests/Pipeline/ParseStageTests.cs`
- `tests/Crucible.Core.Tests/Pipeline/TransformStageTests.cs`
- `tests/Crucible.Core.Tests/Pipeline/InputDetectorTests.cs`
- `tests/Crucible.Extensions.Tests/Crucible.Extensions.Tests.csproj`
- `tests/Crucible.Extensions.Tests/Mermaid/MermaidExtensionTests.cs`

### Test Fixtures
- `tests/Crucible.Core.Tests/Fixtures/` — sample .md files for integration testing

---

## Task 1: Repository Scaffolding

**Files:**
- Create: `Directory.Build.props`
- Create: `Directory.Build.rsp`
- Create: `Directory.Packages.props`
- Create: `Crucible.slnx`
- Create: `.gitignore`
- Create: `LICENSE`
- Create: `README.md`

- [ ] **Step 1: Initialize git repo**

```bash
cd /raid/elvogel/repos/phoenixmldb/crucible
git init
```

- [ ] **Step 2: Create Directory.Build.props**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>false</InvariantGlobalization>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-all</AnalysisLevel>
  </PropertyGroup>

  <PropertyGroup>
    <Authors>Endpoint Systems</Authors>
    <Company>Endpoint Systems</Company>
    <Product>Crucible</Product>
    <Copyright>Copyright © Endpoint Systems 2026. All rights reserved.</Copyright>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <DebugType>embedded</DebugType>
    <DebugSymbols>true</DebugSymbols>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Create Directory.Build.rsp**

```
-nodeReuse:false
```

- [ ] **Step 4: Create Directory.Packages.props**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Markdown parsing -->
    <PackageVersion Include="Markdig" Version="0.40.0" />
    <!-- YAML frontmatter -->
    <PackageVersion Include="YamlDotNet" Version="16.3.0" />
    <!-- XSLT engine -->
    <PackageVersion Include="PhoenixmlDb.Xslt" Version="1.0.0-preview.1" />
    <PackageVersion Include="PhoenixmlDb.Core" Version="1.0.0-preview.1" />
    <PackageVersion Include="PhoenixmlDb.Xdm" Version="1.0.0-preview.1" />
    <PackageVersion Include="PhoenixmlDb.XQuery" Version="1.0.0-preview.1" />
    <!-- Testing -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageVersion Include="coverlet.collector" Version="8.0.0" />
    <PackageVersion Include="FluentAssertions" Version="6.12.2" />
  </ItemGroup>
</Project>
```

Note: PhoenixmlDb package versions may need adjustment based on what's published to NuGet. If using project references to the monorepo during development, replace `PackageReference` with `ProjectReference` in the .csproj files and reference the monorepo projects directly.

- [ ] **Step 5: Create .gitignore**

```
bin/
obj/
*.user
*.suo
.vs/
*.DotSettings.user
TestResults/
BenchmarkDotNet.Artifacts/
dist/
intermediate/
```

- [ ] **Step 6: Create project directories**

```bash
mkdir -p src/Crucible.Core src/Crucible.Extensions src/Crucible.Cli
mkdir -p tests/Crucible.Core.Tests tests/Crucible.Extensions.Tests
```

- [ ] **Step 7: Create Crucible.Core.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Crucible.Core</RootNamespace>
    <Description>Pipeline engine for the Crucible static documentation generator</Description>
    <PackageTags>documentation;markdown;xslt;static-site;xml</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Markdig" />
    <PackageReference Include="YamlDotNet" />
    <PackageReference Include="PhoenixmlDb.Xslt" />
    <PackageReference Include="PhoenixmlDb.Core" />
    <PackageReference Include="PhoenixmlDb.Xdm" />
    <PackageReference Include="PhoenixmlDb.XQuery" />
  </ItemGroup>

</Project>
```

- [ ] **Step 8: Create Crucible.Extensions.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Crucible.Extensions</RootNamespace>
    <Description>Built-in extensions for the Crucible documentation generator</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../Crucible.Core/Crucible.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 9: Create Crucible.Cli.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <RootNamespace>Crucible.Cli</RootNamespace>
    <AssemblyName>crucible</AssemblyName>
    <Description>Command-line static documentation generator powered by XSLT 4.0</Description>
    <Authors>endpointsystems</Authors>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>crucible</ToolCommandName>
    <PackageId>Crucible.Cli</PackageId>
    <NoWarn>$(NoWarn);CA1303</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../Crucible.Core/Crucible.Core.csproj" />
    <ProjectReference Include="../Crucible.Extensions/Crucible.Extensions.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 10: Create test project csproj files**

`tests/Crucible.Core.Tests/Crucible.Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <RootNamespace>Crucible.Core.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Crucible.Core/Crucible.Core.csproj" />
  </ItemGroup>

</Project>
```

`tests/Crucible.Extensions.Tests/Crucible.Extensions.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <RootNamespace>Crucible.Extensions.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Crucible.Core/Crucible.Core.csproj" />
    <ProjectReference Include="../../src/Crucible.Extensions/Crucible.Extensions.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 11: Create Crucible.slnx**

```xml
<Solution>
  <Project Path="src/Crucible.Core/Crucible.Core.csproj" />
  <Project Path="src/Crucible.Extensions/Crucible.Extensions.csproj" />
  <Project Path="src/Crucible.Cli/Crucible.Cli.csproj" />
  <Folder Name="/tests/">
    <Project Path="tests/Crucible.Core.Tests/Crucible.Core.Tests.csproj" />
    <Project Path="tests/Crucible.Extensions.Tests/Crucible.Extensions.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 12: Verify solution builds**

```bash
dotnet restore Crucible.slnx
dotnet build Crucible.slnx
```

Expected: Build succeeds with warnings only about empty projects.

- [ ] **Step 13: Commit**

```bash
git add -A
git commit -m "feat: scaffold Crucible solution with Core, Extensions, and Cli projects"
```

---

## Task 2: Core Models and Extension Interface

**Files:**
- Create: `src/Crucible.Core/Models/DocumentMetadata.cs`
- Create: `src/Crucible.Core/Models/SiteManifest.cs`
- Create: `src/Crucible.Core/Models/CrucibleConfig.cs`
- Create: `src/Crucible.Core/Extensions/ICrucibleExtension.cs`
- Create: `src/Crucible.Core/Parsing/SlugGenerator.cs`
- Test: `tests/Crucible.Core.Tests/Parsing/SlugGeneratorTests.cs`

- [ ] **Step 1: Write SlugGenerator tests**

```csharp
namespace Crucible.Core.Tests.Parsing;

using Crucible.Core.Parsing;
using FluentAssertions;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Installation", "installation")]
    [InlineData("Getting Started", "getting-started")]
    [InlineData("xsl:for-each", "xsl-for-each")]
    [InlineData("Hello, World!", "hello-world")]
    [InlineData("  Multiple   Spaces  ", "multiple-spaces")]
    [InlineData("café", "caf")]  // é is non-ASCII, stripped then trimmed
    [InlineData("", "")]
    [InlineData("Already-Valid", "already-valid")]
    [InlineData("123 Numbers First", "123-numbers-first")]
    public void GenerateSlug_ProducesExpectedOutput(string input, string expected)
    {
        SlugGenerator.Generate(input).Should().Be(expected);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Crucible.Core.Tests -v minimal
```

Expected: FAIL — SlugGenerator class does not exist.

- [ ] **Step 3: Write SlugGenerator**

```csharp
namespace Crucible.Core.Parsing;

using System.Text;
using System.Text.RegularExpressions;

public static partial class SlugGenerator
{
    public static string Generate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var slug = text.ToLowerInvariant();
        // Replace any non-alphanumeric character (including punctuation, spaces) with a hyphen
        slug = NonAlphanumericRegex().Replace(slug, "-");
        // Collapse multiple hyphens into one
        slug = MultipleHyphensRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleHyphensRegex();
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/Crucible.Core.Tests -v minimal
```

Expected: All SlugGenerator tests pass.

- [ ] **Step 5: Write DocumentMetadata model**

```csharp
namespace Crucible.Core.Models;

public sealed class DocumentMetadata
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? Sort { get; set; }
    public DateTime? Updated { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool Draft { get; set; }
    public string? Template { get; set; }

    [YamlDotNet.Serialization.YamlIgnore]
    public Dictionary<string, object?> Extra { get; set; } = [];
}
```

Note: Properties use `{ get; set; }` for YamlDotNet deserialization compatibility. The `YamlIgnore` on `Extra` prevents deserialization errors; extra fields are captured separately during parsing.
```

- [ ] **Step 6: Write SiteManifest models**

```csharp
namespace Crucible.Core.Models;

public sealed class SiteManifest
{
    public required string Title { get; init; }
    public required string BaseUrl { get; init; }
    public List<ISiteNode> Children { get; init; } = [];
}

public interface ISiteNode
{
    string Path { get; }
    string Title { get; }
    int? Sort { get; }
}

public sealed class SitePage : ISiteNode
{
    public required string Path { get; init; }
    public required string Title { get; init; }
    public int? Sort { get; init; }
    public string? Description { get; init; }
    public DateTime? Updated { get; init; }
}

public sealed class SiteSection : ISiteNode
{
    public required string Path { get; init; }
    public required string Title { get; init; }
    public int? Sort { get; init; }
    public List<ISiteNode> Children { get; init; } = [];
}
```

- [ ] **Step 7: Write CrucibleConfig model**

```csharp
namespace Crucible.Core.Models;

public sealed class CrucibleConfig
{
    public string Title { get; set; } = "My Documentation";

    [YamlDotNet.Serialization.YamlMember(Alias = "base-url")]
    public string BaseUrl { get; set; } = "/";

    public string Source { get; set; } = "./docs";
    public string Output { get; set; } = "./dist";
    public string? Theme { get; set; }
    public List<string> Extensions { get; set; } = [];
}
```

Note: `YamlMember(Alias = "base-url")` handles the kebab-case YAML key. Properties use `{ get; set; }` for YamlDotNet compatibility.
```

- [ ] **Step 8: Write ICrucibleExtension interface**

```csharp
namespace Crucible.Core.Extensions;

using System.Xml;
using Crucible.Core.Models;
using Markdig.Syntax;

public interface ICrucibleExtension
{
    string Name { get; }
    bool CanProcess(Type markdigNodeType);
    bool ProcessNode(MarkdownObject node, XmlEmitterContext context);
    IEnumerable<CrucibleAsset> GetAssets();
}

public sealed class XmlEmitterContext
{
    public required XmlWriter Writer { get; init; }
    public required string DocumentPath { get; init; }
    public SiteManifest? Manifest { get; init; }
}

public record CrucibleAsset(string RelativePath, string ContentType, byte[] Content);
```

- [ ] **Step 9: Verify build**

```bash
dotnet build Crucible.slnx
```

Expected: Build succeeds.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat: add core models, extension interface, and slug generator with tests"
```

---

## Task 3: Frontmatter Parser

**Files:**
- Create: `src/Crucible.Core/Parsing/FrontmatterParser.cs`
- Test: `tests/Crucible.Core.Tests/Parsing/FrontmatterParserTests.cs`
- Create: `tests/Crucible.Core.Tests/Fixtures/simple-page.md`
- Create: `tests/Crucible.Core.Tests/Fixtures/no-frontmatter.md`
- Create: `tests/Crucible.Core.Tests/Fixtures/minimal-frontmatter.md`

- [ ] **Step 1: Create test fixture files**

`tests/Crucible.Core.Tests/Fixtures/simple-page.md`:
```markdown
---
title: Installation
description: How to install PhoenixmlDb
sort: 1
updated: 2026-03-15
tags:
  - getting-started
  - setup
---

# Installation

Install via NuGet:

```bash
dotnet add package PhoenixmlDb
```
```

`tests/Crucible.Core.Tests/Fixtures/minimal-frontmatter.md`:
```markdown
---
title: Minimal Page
---

Just a title and some content.
```

`tests/Crucible.Core.Tests/Fixtures/no-frontmatter.md`:
```markdown
# No Frontmatter

This file has no YAML frontmatter at all.
```

- [ ] **Step 2: Write FrontmatterParser tests**

```csharp
namespace Crucible.Core.Tests.Parsing;

using Crucible.Core.Parsing;
using FluentAssertions;

public class FrontmatterParserTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Parse_FullFrontmatter_ExtractsAllFields()
    {
        var (metadata, markdown) = FrontmatterParser.Parse(
            File.ReadAllText(FixturePath("simple-page.md")));

        metadata.Should().NotBeNull();
        metadata!.Title.Should().Be("Installation");
        metadata.Description.Should().Be("How to install PhoenixmlDb");
        metadata.Sort.Should().Be(1);
        metadata.Updated.Should().Be(new DateTime(2026, 3, 15));
        metadata.Tags.Should().BeEquivalentTo(["getting-started", "setup"]);
        markdown.Should().Contain("# Installation");
    }

    [Fact]
    public void Parse_MinimalFrontmatter_DefaultsOptionalFields()
    {
        var (metadata, markdown) = FrontmatterParser.Parse(
            File.ReadAllText(FixturePath("minimal-frontmatter.md")));

        metadata.Should().NotBeNull();
        metadata!.Title.Should().Be("Minimal Page");
        metadata.Description.Should().BeNull();
        metadata.Sort.Should().BeNull();
        metadata.Draft.Should().BeFalse();
        markdown.Should().Contain("Just a title");
    }

    [Fact]
    public void Parse_NoFrontmatter_ReturnsNull()
    {
        var (metadata, markdown) = FrontmatterParser.Parse(
            File.ReadAllText(FixturePath("no-frontmatter.md")));

        metadata.Should().BeNull();
        markdown.Should().Contain("# No Frontmatter");
    }
}
```

- [ ] **Step 3: Ensure fixture files are copied to output**

Add to `tests/Crucible.Core.Tests/Crucible.Core.Tests.csproj`:
```xml
<ItemGroup>
  <None Include="Fixtures/**/*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 4: Run tests to verify they fail**

```bash
dotnet test tests/Crucible.Core.Tests -v minimal
```

Expected: FAIL — FrontmatterParser does not exist.

- [ ] **Step 5: Implement FrontmatterParser**

```csharp
namespace Crucible.Core.Parsing;

using Crucible.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class FrontmatterParser
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Splits a Markdown file into YAML frontmatter metadata and Markdown body.
    /// Returns (null, fullContent) if no frontmatter is present.
    /// </summary>
    public static (DocumentMetadata? Metadata, string Markdown) Parse(string content)
    {
        if (!content.StartsWith("---", StringComparison.Ordinal))
            return (null, content);

        var endIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return (null, content);

        var yamlBlock = content[3..endIndex].Trim();
        var markdown = content[(endIndex + 4)..].TrimStart('\r', '\n');

        var metadata = YamlDeserializer.Deserialize<DocumentMetadata>(yamlBlock);
        return (metadata, markdown);
    }
}
```

Note: `DocumentMetadata` will need YamlDotNet-compatible construction. Adjust the model to use `{ get; set; }` properties instead of `init` for YamlDotNet deserialization, or use a DTO that maps to the immutable model.

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test tests/Crucible.Core.Tests -v minimal
```

Expected: All FrontmatterParser tests pass.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add frontmatter parser with YAML deserialization and tests"
```

---

## Task 4: Markdown to XML Emitter

**Files:**
- Create: `src/Crucible.Core/Parsing/MarkdownToXmlEmitter.cs`
- Test: `tests/Crucible.Core.Tests/Parsing/MarkdownToXmlEmitterTests.cs`

This is the core of Stage 1 — walking the Markdig AST and emitting the Crucible XML schema.

- [ ] **Step 1: Write emitter tests for basic elements**

```csharp
namespace Crucible.Core.Tests.Parsing;

using System.Xml.Linq;
using Crucible.Core.Models;
using Crucible.Core.Parsing;
using FluentAssertions;

public class MarkdownToXmlEmitterTests
{
    private static XDocument Emit(string markdown, string path = "test",
        DocumentMetadata? metadata = null)
    {
        metadata ??= new DocumentMetadata { Title = "Test" };
        var xml = MarkdownToXmlEmitter.Emit(markdown, metadata, path);
        return XDocument.Parse(xml);
    }

    [Fact]
    public void Emit_Heading_ProducesHeadingElement()
    {
        var doc = Emit("# Hello World");
        var heading = doc.Root!.Element("body")!.Element("heading")!;
        heading.Attribute("level")!.Value.Should().Be("1");
        heading.Attribute("id")!.Value.Should().Be("hello-world");
        heading.Value.Should().Be("Hello World");
    }

    [Fact]
    public void Emit_Paragraph_ProducesParagraphElement()
    {
        var doc = Emit("Some text here.");
        doc.Root!.Element("body")!.Element("paragraph")!.Value
            .Should().Be("Some text here.");
    }

    [Fact]
    public void Emit_FencedCodeBlock_ProducesCodeBlockElement()
    {
        var doc = Emit("```csharp\nvar x = 1;\n```");
        var code = doc.Root!.Element("body")!.Element("code-block")!;
        code.Attribute("language")!.Value.Should().Be("csharp");
        code.Value.Should().Contain("var x = 1;");
    }

    [Fact]
    public void Emit_UnorderedList_ProducesListElement()
    {
        var doc = Emit("- Item A\n- Item B");
        var list = doc.Root!.Element("body")!.Element("list")!;
        list.Attribute("type")!.Value.Should().Be("unordered");
        list.Elements("item").Should().HaveCount(2);
    }

    [Fact]
    public void Emit_InlineCode_ProducesCodeElement()
    {
        var doc = Emit("Use `dotnet build` to compile.");
        var para = doc.Root!.Element("body")!.Element("paragraph")!;
        para.Element("code")!.Value.Should().Be("dotnet build");
    }

    [Fact]
    public void Emit_Link_ProducesLinkElement()
    {
        var doc = Emit("[Click here](https://example.com)");
        var link = doc.Root!.Element("body")!.Element("paragraph")!.Element("link")!;
        link.Attribute("href")!.Value.Should().Be("https://example.com");
        link.Value.Should().Be("Click here");
    }

    [Fact]
    public void Emit_DocumentAttributes_IncludesMetadata()
    {
        var metadata = new DocumentMetadata
        {
            Title = "My Page",
            Description = "A test page",
            Updated = new DateTime(2026, 3, 15),
            Tags = ["test", "demo"]
        };
        var doc = Emit("Content", metadata: metadata);

        doc.Root!.Attribute("title")!.Value.Should().Be("My Page");
        doc.Root!.Attribute("description")!.Value.Should().Be("A test page");
        doc.Root!.Element("meta")!.Elements("tag").Should().HaveCount(2);
    }

    [Fact]
    public void Emit_Table_ProducesTableElements()
    {
        var md = "| A | B |\n|---|---|\n| 1 | 2 |";
        var doc = Emit(md);
        var table = doc.Root!.Element("body")!.Element("table")!;
        table.Element("table-head").Should().NotBeNull();
        table.Element("table-body").Should().NotBeNull();
    }

    [Fact]
    public void Emit_Emphasis_ProducesEmphasisElement()
    {
        var doc = Emit("This is *italic* text.");
        doc.Root!.Element("body")!.Element("paragraph")!
            .Element("emphasis")!.Value.Should().Be("italic");
    }

    [Fact]
    public void Emit_Strong_ProducesStrongElement()
    {
        var doc = Emit("This is **bold** text.");
        doc.Root!.Element("body")!.Element("paragraph")!
            .Element("strong")!.Value.Should().Be("bold");
    }

    [Fact]
    public void Emit_Image_ProducesImageElement()
    {
        var doc = Emit("![Alt text](image.png \"Title\")");
        var img = doc.Root!.Element("body")!.Element("paragraph")!.Element("image")!;
        img.Attribute("src")!.Value.Should().Be("image.png");
        img.Attribute("alt")!.Value.Should().Be("Alt text");
        img.Attribute("title")!.Value.Should().Be("Title");
    }

    [Fact]
    public void Emit_Admonition_ProducesAdmonitionElement()
    {
        var doc = Emit("::: note\nThis is important.\n:::");
        var admonition = doc.Root!.Element("body")!.Element("admonition")!;
        admonition.Attribute("type")!.Value.Should().Be("note");
    }

    [Fact]
    public void Emit_Blockquote_ProducesBlockquoteElement()
    {
        var doc = Emit("> This is a quote.");
        doc.Root!.Element("body")!.Element("blockquote").Should().NotBeNull();
    }

    [Fact]
    public void Emit_ThematicBreak_ProducesThematicBreakElement()
    {
        var doc = Emit("Above\n\n***\n\nBelow");
        doc.Root!.Element("body")!.Descendants("thematic-break").Should().HaveCount(1);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Crucible.Core.Tests --filter "MarkdownToXmlEmitter" -v minimal
```

Expected: FAIL — MarkdownToXmlEmitter does not exist.

- [ ] **Step 3: Implement MarkdownToXmlEmitter**

Create `src/Crucible.Core/Parsing/MarkdownToXmlEmitter.cs`:

```csharp
namespace Crucible.Core.Parsing;

using System.Xml;
using Crucible.Core.Extensions;
using Crucible.Core.Models;
using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

public static class MarkdownToXmlEmitter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UsePipeTables()
        .UseCustomContainers()   // ::: admonitions
        .UseTaskLists()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .Build();

    private static Dictionary<Type, ICrucibleExtension> _dispatch = [];

    public static string Emit(string markdown, DocumentMetadata metadata,
        string path, IEnumerable<ICrucibleExtension>? extensions = null)
    {
        // Build extension dispatch table
        _dispatch = [];
        if (extensions != null)
        {
            foreach (var ext in extensions)
            {
                foreach (var type in GetAllMarkdigNodeTypes())
                {
                    if (ext.CanProcess(type))
                        _dispatch[type] = ext; // last wins
                }
            }
        }

        var doc = Markdown.Parse(markdown, Pipeline);
        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false
        };

        using var sw = new StringWriter();
        using var writer = XmlWriter.Create(sw, settings);
        var context = new XmlEmitterContext
        {
            Writer = writer,
            DocumentPath = path
        };

        writer.WriteStartDocument();
        writer.WriteStartElement("document");
        writer.WriteAttributeString("path", path);
        writer.WriteAttributeString("title", metadata.Title);
        if (metadata.Description != null)
            writer.WriteAttributeString("description", metadata.Description);
        if (metadata.Updated.HasValue)
            writer.WriteAttributeString("updated",
                metadata.Updated.Value.ToString("yyyy-MM-dd"));

        // Meta section
        if (metadata.Tags.Count > 0 || metadata.Extra.Count > 0)
        {
            writer.WriteStartElement("meta");
            foreach (var tag in metadata.Tags)
                writer.WriteElementString("tag", tag);
            foreach (var (key, value) in metadata.Extra)
                writer.WriteElementString(key, value?.ToString() ?? "");
            writer.WriteEndElement(); // meta
        }

        // Body
        writer.WriteStartElement("body");
        foreach (var block in doc)
            EmitBlock(block, context);
        writer.WriteEndElement(); // body

        writer.WriteEndElement(); // document
        writer.WriteEndDocument();
        writer.Flush();
        return sw.ToString();
    }

    private static void EmitBlock(Block block, XmlEmitterContext ctx)
    {
        // Check extension dispatch first
        if (_dispatch.TryGetValue(block.GetType(), out var ext))
        {
            if (ext.ProcessNode(block, ctx))
                return; // extension handled it and its children
        }

        switch (block)
        {
            case HeadingBlock heading:
                ctx.Writer.WriteStartElement("heading");
                ctx.Writer.WriteAttributeString("level",
                    heading.Level.ToString());
                var text = heading.Inline?.FirstChild?.ToString() ?? "";
                ctx.Writer.WriteAttributeString("id",
                    SlugGenerator.Generate(text));
                EmitInlines(heading.Inline, ctx);
                ctx.Writer.WriteEndElement();
                break;

            case ParagraphBlock para:
                ctx.Writer.WriteStartElement("paragraph");
                EmitInlines(para.Inline, ctx);
                ctx.Writer.WriteEndElement();
                break;

            case FencedCodeBlock fenced:
                ctx.Writer.WriteStartElement("code-block");
                if (!string.IsNullOrEmpty(fenced.Info))
                    ctx.Writer.WriteAttributeString("language", fenced.Info);
                ctx.Writer.WriteString(ExtractLines(fenced));
                ctx.Writer.WriteEndElement();
                break;

            case CodeBlock code:
                ctx.Writer.WriteStartElement("code-block");
                ctx.Writer.WriteString(ExtractLines(code));
                ctx.Writer.WriteEndElement();
                break;

            case ListBlock list:
                ctx.Writer.WriteStartElement("list");
                ctx.Writer.WriteAttributeString("type",
                    list.IsOrdered ? "ordered" : "unordered");
                foreach (var item in list)
                {
                    ctx.Writer.WriteStartElement("item");
                    if (item is ListItemBlock listItem)
                    {
                        foreach (var child in listItem)
                            EmitBlock(child, ctx);
                    }
                    ctx.Writer.WriteEndElement();
                }
                ctx.Writer.WriteEndElement();
                break;

            case QuoteBlock quote:
                ctx.Writer.WriteStartElement("blockquote");
                foreach (var child in quote)
                    EmitBlock(child, ctx);
                ctx.Writer.WriteEndElement();
                break;

            case Table table:
                EmitTable(table, ctx);
                break;

            case ThematicBreakBlock:
                ctx.Writer.WriteStartElement("thematic-break");
                ctx.Writer.WriteEndElement();
                break;

            case CustomContainer container:
                EmitAdmonition(container, ctx);
                break;

            default:
                // For any unrecognized block with children, descend
                if (block is ContainerBlock containerBlock)
                {
                    foreach (var child in containerBlock)
                        EmitBlock(child, ctx);
                }
                break;
        }
    }

    private static void EmitInlines(ContainerInline? container,
        XmlEmitterContext ctx)
    {
        if (container == null) return;
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    ctx.Writer.WriteString(literal.Content.ToString());
                    break;
                case EmphasisInline emphasis:
                    var elem = emphasis.DelimiterCount == 2 ? "strong" : "emphasis";
                    ctx.Writer.WriteStartElement(elem);
                    EmitInlines(emphasis, ctx);
                    ctx.Writer.WriteEndElement();
                    break;
                case CodeInline code:
                    ctx.Writer.WriteStartElement("code");
                    ctx.Writer.WriteString(code.Content);
                    ctx.Writer.WriteEndElement();
                    break;
                case LinkInline link:
                    if (link.IsImage)
                    {
                        ctx.Writer.WriteStartElement("image");
                        ctx.Writer.WriteAttributeString("src", link.Url ?? "");
                        ctx.Writer.WriteAttributeString("alt",
                            link.FirstChild?.ToString() ?? "");
                        if (link.Title != null)
                            ctx.Writer.WriteAttributeString("title", link.Title);
                        ctx.Writer.WriteEndElement();
                    }
                    else
                    {
                        ctx.Writer.WriteStartElement("link");
                        ctx.Writer.WriteAttributeString("href", link.Url ?? "");
                        if (link.Title != null)
                            ctx.Writer.WriteAttributeString("title", link.Title);
                        EmitInlines(link, ctx);
                        ctx.Writer.WriteEndElement();
                    }
                    break;
                case LineBreakInline:
                    ctx.Writer.WriteString("\n");
                    break;
                case ContainerInline ci:
                    EmitInlines(ci, ctx);
                    break;
            }
        }
    }

    private static void EmitTable(Table table, XmlEmitterContext ctx)
    {
        ctx.Writer.WriteStartElement("table");
        var isFirstRow = true;
        var inHead = false;
        foreach (var row in table.OfType<TableRow>())
        {
            if (row.IsHeader && !inHead)
            {
                ctx.Writer.WriteStartElement("table-head");
                inHead = true;
            }
            else if (!row.IsHeader && inHead)
            {
                ctx.Writer.WriteEndElement(); // table-head
                ctx.Writer.WriteStartElement("table-body");
                inHead = false;
            }
            else if (!row.IsHeader && isFirstRow)
            {
                ctx.Writer.WriteStartElement("table-body");
            }

            ctx.Writer.WriteStartElement("row");
            foreach (var cell in row.OfType<TableCell>())
            {
                ctx.Writer.WriteStartElement("cell");
                if (row.IsHeader)
                    ctx.Writer.WriteAttributeString("header", "true");
                foreach (var child in cell)
                    EmitBlock(child, ctx);
                ctx.Writer.WriteEndElement(); // cell
            }
            ctx.Writer.WriteEndElement(); // row
            isFirstRow = false;
        }
        if (inHead)
            ctx.Writer.WriteEndElement(); // table-head (no body rows)
        else
            ctx.Writer.WriteEndElement(); // table-body
        ctx.Writer.WriteEndElement(); // table
    }

    private static void EmitAdmonition(CustomContainer container,
        XmlEmitterContext ctx)
    {
        var type = container.Info?.Trim().ToLowerInvariant() ?? "note";
        ctx.Writer.WriteStartElement("admonition");
        ctx.Writer.WriteAttributeString("type", type);
        foreach (var child in container)
            EmitBlock(child, ctx);
        ctx.Writer.WriteEndElement();
    }

    private static string ExtractLines(LeafBlock block)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var line in block.Lines)
        {
            if (line.Slice.Length > 0)
                sb.AppendLine(line.Slice.ToString());
        }
        return sb.ToString().TrimEnd();
    }

    private static IEnumerable<Type> GetAllMarkdigNodeTypes()
    {
        // Return common Markdig node types for extension dispatch
        return [
            typeof(HeadingBlock), typeof(ParagraphBlock),
            typeof(FencedCodeBlock), typeof(CodeBlock),
            typeof(ListBlock), typeof(QuoteBlock),
            typeof(Table), typeof(ThematicBreakBlock),
            typeof(CustomContainer)
        ];
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Crucible.Core.Tests --filter "MarkdownToXmlEmitter" -v minimal
```

Expected: All emitter tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add Markdown to XML emitter with full element mapping and tests"
```

---

## Task 5: Link Resolver

**Files:**
- Create: `src/Crucible.Core/Parsing/LinkResolver.cs`
- Test: `tests/Crucible.Core.Tests/Parsing/LinkResolverTests.cs`

- [ ] **Step 1: Write LinkResolver tests**

```csharp
namespace Crucible.Core.Tests.Parsing;

using Crucible.Core.Parsing;
using FluentAssertions;

public class LinkResolverTests
{
    [Fact]
    public void Resolve_RelativeMdLink_RewritesToHtml()
    {
        var resolver = new LinkResolver(
            ["getting-started/installation", "getting-started/quick-start"]);

        var result = resolver.Resolve("../getting-started/installation.md",
            currentPath: "xslt/overview");

        result.ResolvedHref.Should().Be("../getting-started/installation.html");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_RootRelativeMdLink_RewritesToHtml()
    {
        var resolver = new LinkResolver(
            ["getting-started/installation"]);

        var result = resolver.Resolve("/getting-started/installation.md",
            currentPath: "xslt/deep/nested/page");

        result.ResolvedHref.Should().Be("/getting-started/installation.html");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_BrokenLink_MarksAsBroken()
    {
        var resolver = new LinkResolver(["index"]);

        var result = resolver.Resolve("nonexistent.md", currentPath: "index");

        result.IsBroken.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ExternalLink_PassesThrough()
    {
        var resolver = new LinkResolver([]);

        var result = resolver.Resolve("https://example.com", currentPath: "index");

        result.ResolvedHref.Should().Be("https://example.com");
        result.IsBroken.Should().BeFalse();
    }

    [Fact]
    public void Resolve_AnchorLink_PreservesFragment()
    {
        var resolver = new LinkResolver(
            ["getting-started/installation"]);

        var result = resolver.Resolve("installation.md#requirements",
            currentPath: "getting-started/overview");

        result.ResolvedHref.Should().Be("installation.html#requirements");
        result.IsBroken.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Crucible.Core.Tests --filter "LinkResolver" -v minimal
```

Expected: FAIL.

- [ ] **Step 3: Implement LinkResolver**

```csharp
namespace Crucible.Core.Parsing;

public sealed class LinkResolver
{
    private readonly HashSet<string> _knownPaths;

    public LinkResolver(IEnumerable<string> knownPaths)
    {
        _knownPaths = new HashSet<string>(knownPaths, StringComparer.OrdinalIgnoreCase);
    }

    public LinkResult Resolve(string href, string currentPath)
    {
        // External links pass through
        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return new LinkResult(href, IsBroken: false);
        }

        // Split fragment
        string? fragment = null;
        var fragmentIdx = href.IndexOf('#', StringComparison.Ordinal);
        if (fragmentIdx >= 0)
        {
            fragment = href[fragmentIdx..];
            href = href[..fragmentIdx];
        }

        // Not a .md link — pass through
        if (!href.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return new LinkResult(href + (fragment ?? ""), IsBroken: false);

        // Rewrite .md → .html
        var htmlHref = href[..^3] + ".html";

        // Check if target exists (resolve path for validation)
        var targetPath = ResolvePath(href[..^3], currentPath, href.StartsWith('/'));
        var isBroken = targetPath != null && !_knownPaths.Contains(targetPath);

        return new LinkResult(htmlHref + (fragment ?? ""), isBroken);
    }

    private static string? ResolvePath(string target, string currentPath, bool isRootRelative)
    {
        if (isRootRelative)
            return target.TrimStart('/');

        var currentDir = currentPath.Contains('/')
            ? currentPath[..currentPath.LastIndexOf('/')]
            : "";

        var combined = string.IsNullOrEmpty(currentDir)
            ? target
            : $"{currentDir}/{target}";

        // Normalize . and ..
        var segments = combined.Split('/').ToList();
        var result = new List<string>();
        foreach (var seg in segments)
        {
            if (seg == ".") continue;
            if (seg == ".." && result.Count > 0) { result.RemoveAt(result.Count - 1); continue; }
            result.Add(seg);
        }

        return string.Join("/", result);
    }
}

public record LinkResult(string ResolvedHref, bool IsBroken);
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Crucible.Core.Tests --filter "LinkResolver" -v minimal
```

Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add link resolver with relative, root-relative, and external link handling"
```

---

## Task 6: Site Manifest Builder

**Files:**
- Create: `src/Crucible.Core/Manifest/SiteManifestBuilder.cs`
- Test: `tests/Crucible.Core.Tests/Manifest/SiteManifestBuilderTests.cs`
- Create: `tests/Crucible.Core.Tests/Fixtures/sample-site/` (directory tree with .md files)

- [ ] **Step 1: Create sample site fixture**

Create a directory tree under `tests/Crucible.Core.Tests/Fixtures/sample-site/`:
```
sample-site/
├── index.md          (title: Home, sort: 0)
├── getting-started/
│   ├── installation.md  (title: Installation, sort: 1)
│   └── quick-start.md   (title: Quick Start, sort: 2)
└── reference/
    └── api.md           (title: API Reference)
```

Each `.md` file has YAML frontmatter with title and optional sort.

- [ ] **Step 2: Write SiteManifestBuilder tests**

```csharp
namespace Crucible.Core.Tests.Manifest;

using Crucible.Core.Manifest;
using Crucible.Core.Models;
using FluentAssertions;

public class SiteManifestBuilderTests
{
    private static string FixtureDir(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Build_SampleSite_ProducesCorrectStructure()
    {
        var manifest = SiteManifestBuilder.Build(FixtureDir("sample-site"),
            title: "Test Site", baseUrl: "/");

        manifest.Title.Should().Be("Test Site");
        manifest.Children.Should().HaveCount(3); // index + 2 sections

        var index = manifest.Children.OfType<SitePage>().First();
        index.Title.Should().Be("Home");
        index.Sort.Should().Be(0);
    }

    [Fact]
    public void Build_SortOrder_SortedPagesBeforeUnsorted()
    {
        var manifest = SiteManifestBuilder.Build(FixtureDir("sample-site"),
            title: "Test", baseUrl: "/");

        // Children should be: index (sort=0), getting-started section, reference section
        // getting-started has sorted pages, reference has unsorted
        var gettingStarted = manifest.Children.OfType<SiteSection>()
            .First(s => s.Path.Contains("getting-started"));
        gettingStarted.Children.Should().HaveCount(2);
        gettingStarted.Children[0].Title.Should().Be("Installation"); // sort=1
        gettingStarted.Children[1].Title.Should().Be("Quick Start");  // sort=2
    }

    [Fact]
    public void Build_DirectoryWithoutIndex_InfersTitleFromDirName()
    {
        var manifest = SiteManifestBuilder.Build(FixtureDir("sample-site"),
            title: "Test", baseUrl: "/");

        var reference = manifest.Children.OfType<SiteSection>()
            .First(s => s.Path.Contains("reference"));
        reference.Title.Should().Be("Reference");
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test tests/Crucible.Core.Tests --filter "SiteManifestBuilder" -v minimal
```

- [ ] **Step 4: Implement SiteManifestBuilder**

Scans directories recursively, reads frontmatter from each `.md` file, builds the `SiteManifest` tree. Sort order: pages with explicit `sort` first (ascending), then unsorted pages alphabetically by filename. Section titles inferred from `index.md` frontmatter if present, otherwise title-cased from directory name.

- [ ] **Step 5: Write manifest XML serialization**

Add a `ToXml()` method on `SiteManifest` (or a static serializer class) that produces the `site-manifest.xml` format from the spec. Include the `updated` attribute on `<page>` elements when available (needed by `sitemap.xslt` for `<lastmod>`).

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test tests/Crucible.Core.Tests --filter "SiteManifestBuilder" -v minimal
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add site manifest builder with directory scanning and sort ordering"
```

---

## Task 7: Parse Stage (Stage 1 Orchestrator)

**Files:**
- Create: `src/Crucible.Core/Pipeline/ParseStage.cs`
- Create: `src/Crucible.Core/Pipeline/BuildResult.cs`
- Create: `src/Crucible.Core/Pipeline/InputDetector.cs`
- Test: `tests/Crucible.Core.Tests/Pipeline/ParseStageTests.cs`
- Test: `tests/Crucible.Core.Tests/Pipeline/InputDetectorTests.cs`

- [ ] **Step 1: Write InputDetector tests**

```csharp
namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Pipeline;
using FluentAssertions;

public class InputDetectorTests
{
    [Fact]
    public void Detect_DirectoryWithManifest_ReturnsXmlIntermediate()
    {
        // Create temp dir with site-manifest.xml
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site-manifest.xml"), "<site/>");

        try
        {
            InputDetector.Detect(dir).Should().Be(InputType.XmlIntermediate);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Detect_DirectoryWithMarkdown_ReturnsMarkdownSource()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.md"), "# Hello");

        try
        {
            InputDetector.Detect(dir).Should().Be(InputType.MarkdownSource);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Implement InputDetector**

```csharp
namespace Crucible.Core.Pipeline;

public enum InputType { MarkdownSource, XmlIntermediate }

public static class InputDetector
{
    public static InputType Detect(string directory) =>
        File.Exists(Path.Combine(directory, "site-manifest.xml"))
            ? InputType.XmlIntermediate
            : InputType.MarkdownSource;
}
```

- [ ] **Step 3: Write BuildResult**

```csharp
namespace Crucible.Core.Pipeline;

using System.Diagnostics;

public sealed class BuildResult
{
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool Success => Errors.Count == 0;
    public Stopwatch? ParseTiming { get; set; }
    public Stopwatch? TransformTiming { get; set; }
}
```

- [ ] **Step 4: Write ParseStage integration test**

```csharp
namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Pipeline;
using FluentAssertions;

public class ParseStageTests
{
    [Fact]
    public async Task Execute_SampleSite_ProducesXmlFiles()
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-site");
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var result = await ParseStage.ExecuteAsync(sourceDir, outputDir,
                title: "Test Site", baseUrl: "/",
                extensions: [], includeDrafts: false);

            result.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "site-manifest.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "index.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "getting-started", "installation.xml"))
                .Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }
}
```

- [ ] **Step 5: Implement ParseStage**

Orchestrates:
1. Discover `.md` files in source directory
2. Parse each with `FrontmatterParser`
3. Skip files with `draft: true` (unless include-drafts)
4. Collect errors for files missing `title`
5. Build site manifest via `SiteManifestBuilder`
6. Create `LinkResolver` with known paths
7. Emit XML for each file via `MarkdownToXmlEmitter`
8. Write `site-manifest.xml` and per-document `.xml` files to output
9. Return `BuildResult`

- [ ] **Step 6: Run tests**

```bash
dotnet test tests/Crucible.Core.Tests --filter "ParseStage|InputDetector" -v minimal
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add parse stage orchestrator with input detection and build result"
```

---

## Task 8: Default Theme XSLT

**Files:**
- Create: `src/Crucible.Core/Themes/default/page.xslt`
- Create: `src/Crucible.Core/Themes/default/navigation.xslt`
- Create: `src/Crucible.Core/Themes/default/sitemap.xslt`
- Create: `src/Crucible.Core/Themes/default/css/style.css`
- Create: `src/Crucible.Core/Themes/default/js/theme.js`
- Create: `src/Crucible.Core/Themes/ThemeLoader.cs`

- [ ] **Step 1: Create page.xslt**

The main XSLT stylesheet that transforms document XML to HTML5. Key features:
- `xsl:output method="html" version="5"` with `indent="yes"`
- Parameter `site-manifest-path` — path to site-manifest.xml, loaded via `doc()`
- Matches `<document>` root: emits `<!DOCTYPE html>`, `<html>`, `<head>` (with SEO meta), `<body>`
- `<head>` includes: `<title>`, `<meta name="description">`, `<link rel="canonical">`, Open Graph tags, CSS link
- `<body>` structure: `<header>` (site title), `<nav>` (calls navigation.xslt), `<main><article>` (content), `<footer>`
- Template matches for each body element: `heading` → `<h1>`-`<h6>` with anchor, `paragraph` → `<p>`, `code-block` → `<pre><code>`, `list` → `<ul>`/`<ol>`, etc.
- `<mermaid>` → `<div class="mermaid"><pre class="mermaid">` for client-side rendering

This is real, working XSLT 4.0 that demonstrates the PhoenixmlDb engine. Write it carefully — it's both functional and a showcase.

- [ ] **Step 2: Create navigation.xslt**

Imported by page.xslt. Templates that process the site manifest:
- Sidebar: recursive template matching `section` and `page` elements
- Breadcrumbs: trace path from root to current page
- Previous/next links: linear traversal of page order

- [ ] **Step 3: Create sitemap.xslt**

Transforms site-manifest.xml into a valid sitemaps.org `sitemap.xml`:
```xslt
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="4.0">
  <xsl:output method="xml" indent="yes"/>
  <xsl:template match="site">
    <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
      <xsl:apply-templates select=".//page"/>
    </urlset>
  </xsl:template>
  <xsl:template match="page">
    <url>
      <loc><xsl:value-of select="concat(ancestor::site/@base-url, @path, '.html')"/></loc>
      <xsl:if test="@updated">
        <lastmod><xsl:value-of select="@updated"/></lastmod>
      </xsl:if>
    </url>
  </xsl:template>
</xsl:stylesheet>
```

- [ ] **Step 4: Create default CSS**

Clean, responsive stylesheet with:
- CSS custom properties for colors, fonts, spacing
- Responsive layout (sidebar collapses on mobile)
- Code block styling with syntax highlighting classes
- Admonition styling (note, warning, tip, important, caution)
- Table styling
- Navigation sidebar styling
- Print styles

- [ ] **Step 5: Create theme.js**

Minimal JS:
- Mobile navigation toggle (hamburger menu)
- Smooth scroll for anchor links
- No framework dependencies

- [ ] **Step 6: Create ThemeLoader**

```csharp
namespace Crucible.Core.Themes;

public sealed class ThemeLoader
{
    public string PageXslt { get; }
    public string SitemapXslt { get; }
    public string ThemeDirectory { get; }

    public ThemeLoader(string? customThemePath = null)
    {
        ThemeDirectory = customThemePath ?? GetDefaultThemePath();
        PageXslt = File.ReadAllText(Path.Combine(ThemeDirectory, "page.xslt"));
        SitemapXslt = File.ReadAllText(Path.Combine(ThemeDirectory, "sitemap.xslt"));
    }

    public IEnumerable<(string RelativePath, string FullPath)> GetStaticAssets()
    {
        var cssDir = Path.Combine(ThemeDirectory, "css");
        var jsDir = Path.Combine(ThemeDirectory, "js");
        // Yield all files from css/ and js/ directories
        foreach (var dir in new[] { cssDir, jsDir })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                yield return (Path.GetRelativePath(ThemeDirectory, file), file);
            }
        }
    }

    private static string GetDefaultThemePath()
    {
        // Embedded in the assembly's output directory
        return Path.Combine(AppContext.BaseDirectory, "Themes", "default");
    }
}
```

- [ ] **Step 7: Ensure theme files are embedded in output**

Add to `src/Crucible.Core/Crucible.Core.csproj`:
```xml
<ItemGroup>
  <None Include="Themes/**/*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 8: Verify build**

```bash
dotnet build Crucible.slnx
```

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: add default theme with XSLT 4.0 templates, CSS, and JS"
```

---

## Task 9: Transform Stage (Stage 2 Orchestrator)

**Files:**
- Create: `src/Crucible.Core/Pipeline/TransformStage.cs`
- Test: `tests/Crucible.Core.Tests/Pipeline/TransformStageTests.cs`

- [ ] **Step 1: Write TransformStage test**

```csharp
namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Pipeline;
using FluentAssertions;

public class TransformStageTests
{
    [Fact]
    public async Task Execute_XmlIntermediate_ProducesHtmlFiles()
    {
        // First run parse stage to get XML intermediate
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var parseResult = await ParseStage.ExecuteAsync(sourceDir, intermediateDir,
                title: "Test Site", baseUrl: "/",
                extensions: [], includeDrafts: false);
            parseResult.Success.Should().BeTrue();

            var transformResult = await TransformStage.ExecuteAsync(
                intermediateDir, outputDir,
                themePath: null, extensions: []);
            transformResult.Success.Should().BeTrue();

            File.Exists(Path.Combine(outputDir, "index.html")).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "sitemap.xml")).Should().BeTrue();

            var html = await File.ReadAllTextAsync(
                Path.Combine(outputDir, "index.html"));
            html.Should().Contain("<html");
            html.Should().Contain("<title>");
        }
        finally
        {
            if (Directory.Exists(intermediateDir))
                Directory.Delete(intermediateDir, recursive: true);
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Crucible.Core.Tests --filter "TransformStage" -v minimal
```

- [ ] **Step 3: Implement TransformStage**

Orchestrates:
1. Load theme via `ThemeLoader`
2. Read `site-manifest.xml` from input directory
3. For each `.xml` document file:
   a. Create `XsltTransformer`, load `page.xslt`
   b. Set parameter `site-manifest-path` pointing to the manifest file
   c. Set parameter `current-path` for navigation highlighting
   d. Transform document XML → HTML string
   e. Write `.html` file to output directory
4. Generate `sitemap.xml` by transforming manifest with `sitemap.xslt`
5. Copy theme static assets (CSS, JS) to output
6. Copy extension assets to output
7. Return `BuildResult`

Key: Use `XsltTransformer` from PhoenixmlDb.Xslt — the same API pattern used in the CLI tools.

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/Crucible.Core.Tests --filter "TransformStage" -v minimal
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add transform stage with XSLT rendering and sitemap generation"
```

---

## Task 10: Build Pipeline (Full Orchestrator)

**Files:**
- Create: `src/Crucible.Core/Pipeline/BuildPipeline.cs`

- [ ] **Step 1: Implement BuildPipeline**

```csharp
namespace Crucible.Core.Pipeline;

using System.Diagnostics;
using Crucible.Core.Extensions;
using Crucible.Core.Models;

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
                _extensions, _options.IncludeDrafts, ct);

            result.Errors.AddRange(parseResult.Errors);
            result.Warnings.AddRange(parseResult.Warnings);
            parseSw.Stop();
            result.ParseTiming = parseSw;

            if (!result.Success || _options.Stage == BuildStage.ParseOnly)
                return result;

            // Continue to transform with the intermediate output
            var transformSw = Stopwatch.StartNew();
            var transformResult = await TransformStage.ExecuteAsync(
                parseOutput, _config.Output, _config.Theme, _extensions, ct);
            result.Errors.AddRange(transformResult.Errors);
            result.Warnings.AddRange(transformResult.Warnings);
            transformSw.Stop();
            result.TransformTiming = transformSw;
        }
        else if (inputType == InputType.XmlIntermediate)
        {
            var transformSw = Stopwatch.StartNew();
            var transformResult = await TransformStage.ExecuteAsync(
                _config.Source, _config.Output, _config.Theme, _extensions, ct);
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

public enum BuildStage { Full, ParseOnly, TransformOnly }
```

- [ ] **Step 2: Verify build**

```bash
dotnet build Crucible.slnx
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add full build pipeline orchestrator"
```

---

## Task 11: Mermaid Extension

**Files:**
- Create: `src/Crucible.Extensions/Mermaid/MermaidExtension.cs`
- Create: `src/Crucible.Extensions/ExtensionRegistry.cs`
- Test: `tests/Crucible.Extensions.Tests/Mermaid/MermaidExtensionTests.cs`

- [ ] **Step 1: Write MermaidExtension tests**

```csharp
namespace Crucible.Extensions.Tests.Mermaid;

using System.Text;
using System.Xml;
using Crucible.Core.Extensions;
using Crucible.Extensions.Mermaid;
using FluentAssertions;
using Markdig;
using Markdig.Syntax;

public class MermaidExtensionTests
{
    [Fact]
    public void CanProcess_FencedCodeBlock_ReturnsTrue()
    {
        var ext = new MermaidExtension();
        ext.CanProcess(typeof(FencedCodeBlock)).Should().BeTrue();
    }

    [Fact]
    public void CanProcess_OtherNodeType_ReturnsFalse()
    {
        var ext = new MermaidExtension();
        ext.CanProcess(typeof(ParagraphBlock)).Should().BeFalse();
    }

    [Fact]
    public void ProcessNode_MermaidBlock_EmitsMermaidElement()
    {
        var md = "```mermaid\ngraph LR; A-->B;\n```";
        var doc = Markdown.Parse(md);
        var block = doc.Descendants<FencedCodeBlock>().First();

        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = true });
        var context = new XmlEmitterContext
        {
            Writer = writer,
            DocumentPath = "test"
        };

        var ext = new MermaidExtension();
        var handled = ext.ProcessNode(block, context);

        writer.Flush();
        handled.Should().BeTrue();
        sb.ToString().Should().Contain("<mermaid>");
        sb.ToString().Should().Contain("graph LR");
    }

    [Fact]
    public void ProcessNode_NonMermaidCodeBlock_ReturnsFalse()
    {
        var md = "```csharp\nvar x = 1;\n```";
        var doc = Markdown.Parse(md);
        var block = doc.Descendants<FencedCodeBlock>().First();

        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb);
        var context = new XmlEmitterContext
        {
            Writer = writer,
            DocumentPath = "test"
        };

        var ext = new MermaidExtension();
        ext.ProcessNode(block, context).Should().BeFalse();
    }

    [Fact]
    public void GetAssets_ReturnsMermaidJsInitializer()
    {
        var ext = new MermaidExtension();
        var assets = ext.GetAssets().ToList();
        assets.Should().ContainSingle();
        assets[0].RelativePath.Should().Contain("mermaid");
        assets[0].ContentType.Should().Be("application/javascript");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Crucible.Extensions.Tests -v minimal
```

- [ ] **Step 3: Implement MermaidExtension**

```csharp
namespace Crucible.Extensions.Mermaid;

using System.Text;
using Crucible.Core.Extensions;
using Markdig.Syntax;

public sealed class MermaidExtension : ICrucibleExtension
{
    public string Name => "Mermaid";

    public bool CanProcess(Type markdigNodeType) =>
        markdigNodeType == typeof(FencedCodeBlock);

    public bool ProcessNode(MarkdownObject node, XmlEmitterContext context)
    {
        if (node is not FencedCodeBlock fenced)
            return false;

        var info = fenced.Info?.Trim();
        if (!string.Equals(info, "mermaid", StringComparison.OrdinalIgnoreCase))
            return false;

        var content = ExtractContent(fenced);
        context.Writer.WriteStartElement("mermaid");
        context.Writer.WriteString(content);
        context.Writer.WriteEndElement();
        return true;
    }

    public IEnumerable<CrucibleAsset> GetAssets()
    {
        var script = """
            document.addEventListener('DOMContentLoaded', function() {
                if (typeof mermaid !== 'undefined') {
                    mermaid.initialize({ startOnLoad: true, theme: 'default' });
                }
            });
            """u8;
        yield return new CrucibleAsset("js/mermaid-init.js",
            "application/javascript", script.ToArray());
    }

    private static string ExtractContent(FencedCodeBlock block)
    {
        var sb = new StringBuilder();
        foreach (var line in block.Lines)
        {
            if (line.Slice.Length > 0)
                sb.AppendLine(line.Slice.ToString());
        }
        return sb.ToString().TrimEnd();
    }
}
```

- [ ] **Step 4: Implement ExtensionRegistry**

```csharp
namespace Crucible.Extensions;

using Crucible.Core.Extensions;
using Crucible.Extensions.Mermaid;

public static class ExtensionRegistry
{
    public static List<ICrucibleExtension> GetDefaultExtensions()
    {
        return [new MermaidExtension()];
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Crucible.Extensions.Tests -v minimal
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add Mermaid extension with client-side JS rendering"
```

---

## Task 12: CLI — Init Command

**Files:**
- Create: `src/Crucible.Cli/InitCommand.cs`

- [ ] **Step 1: Implement InitCommand**

```csharp
namespace Crucible.Cli;

public static class InitCommand
{
    public static async Task<int> ExecuteAsync(bool force)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "crucible.yaml");

        if (File.Exists(configPath) && !force)
        {
            await Console.Error.WriteLineAsync(
                "crucible.yaml already exists. Use --force to overwrite.")
                .ConfigureAwait(true);
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

        // Scaffold starter docs if source dir doesn't exist
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
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/Crucible.Cli
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add crucible init command with config and starter docs scaffolding"
```

---

## Task 13: CLI — Build Command and Program.cs

**Files:**
- Create: `src/Crucible.Cli/BuildCommand.cs`
- Create: `src/Crucible.Cli/Program.cs`

- [ ] **Step 1: Implement BuildCommand**

Parses CLI flags, loads `crucible.yaml` if present, creates `CrucibleConfig`, assembles extensions, creates `BuildPipeline`, runs it, prints results. Follows the same CLI patterns as `PhoenixmlDb.Xslt.Cli/Program.cs` — manual arg parsing with `file sealed class CliOptions`, `.ConfigureAwait(true)` on all async calls, timing output to stderr, exit codes matching spec.

- [ ] **Step 2: Implement Program.cs**

Top-level statements entry point that routes to `init` or `build` commands:

```csharp
if (args.Length > 0 && args[0] == "init")
    return await InitCommand.ExecuteAsync(/* parse --force */);

return await BuildCommand.ExecuteAsync(args);
```

Include `--version` and `--help` handling at the top level.

- [ ] **Step 3: Smoke test the CLI**

```bash
cd /tmp && mkdir crucible-test && cd crucible-test
dotnet run --project /raid/elvogel/repos/phoenixmldb/crucible/src/Crucible.Cli -- init
dotnet run --project /raid/elvogel/repos/phoenixmldb/crucible/src/Crucible.Cli -- build
ls dist/
```

Expected: `crucible.yaml` created, `docs/index.md` scaffolded, `dist/` contains `index.html` and `sitemap.xml`.

- [ ] **Step 4: Clean up test directory**

```bash
rm -rf /tmp/crucible-test
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add CLI build command and program entry point"
```

---

## Task 14: Plugin Loader (Extension Discovery)

**Files:**
- Create: `src/Crucible.Extensions/PluginLoader.cs`

- [ ] **Step 1: Implement PluginLoader**

```csharp
namespace Crucible.Extensions;

using System.Reflection;
using System.Runtime.Loader;
using Crucible.Core.Extensions;

public static class PluginLoader
{
    public static List<ICrucibleExtension> LoadPlugins(string? pluginsDir,
        IEnumerable<string>? configExtensions = null)
    {
        var extensions = new List<ICrucibleExtension>();
        var assemblyPaths = new List<string>();

        // Collect assemblies from plugins/ directory
        if (pluginsDir != null && Directory.Exists(pluginsDir))
            assemblyPaths.AddRange(Directory.EnumerateFiles(pluginsDir, "*.dll"));

        // Collect assemblies from config extensions list (by assembly name)
        if (configExtensions != null)
        {
            foreach (var name in configExtensions)
            {
                // Try to find the assembly in the plugins directory or by name
                var dllPath = pluginsDir != null
                    ? Path.Combine(pluginsDir, $"{name}.dll")
                    : null;
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
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to load plugin {dll}: {ex.Message}");
            }
        }

        return extensions;
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build Crucible.slnx
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add plugin loader with AssemblyLoadContext isolation"
```

---

## Task 15: End-to-End Integration Test

**Files:**
- Create: `tests/Crucible.Core.Tests/Pipeline/EndToEndTests.cs`
- Create: `tests/Crucible.Core.Tests/Fixtures/full-site/` (more complete test site)

- [ ] **Step 1: Create full-site fixture**

A more complete test site with:
- `index.md` — homepage with links to other pages
- `getting-started/installation.md` — includes code blocks, lists, admonition
- `getting-started/quick-start.md` — includes internal links, mermaid diagram
- `reference/api.md` — includes table, blockquote

- [ ] **Step 2: Write end-to-end test**

```csharp
namespace Crucible.Core.Tests.Pipeline;

using Crucible.Core.Models;
using Crucible.Core.Pipeline;
using Crucible.Extensions;
using FluentAssertions;

public class EndToEndTests
{
    [Fact]
    public async Task FullBuild_ProducesValidStaticSite()
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "full-site");
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var config = new CrucibleConfig
            {
                Title = "Test Documentation",
                BaseUrl = "https://test.example.com",
                Source = sourceDir,
                Output = outputDir
            };

            var pipeline = new BuildPipeline(config,
                ExtensionRegistry.GetDefaultExtensions(),
                new BuildOptions());

            var result = await pipeline.ExecuteAsync();

            result.Success.Should().BeTrue(
                because: string.Join(", ", result.Errors));

            // HTML files exist
            File.Exists(Path.Combine(outputDir, "index.html")).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "getting-started", "installation.html"))
                .Should().BeTrue();

            // Sitemap exists
            File.Exists(Path.Combine(outputDir, "sitemap.xml")).Should().BeTrue();

            // CSS/JS assets copied
            File.Exists(Path.Combine(outputDir, "css", "style.css")).Should().BeTrue();

            // HTML content is valid
            var indexHtml = await File.ReadAllTextAsync(
                Path.Combine(outputDir, "index.html"));
            indexHtml.Should().Contain("<!DOCTYPE html>");
            indexHtml.Should().Contain("<html");
            indexHtml.Should().Contain("og:title");
            indexHtml.Should().Contain("<nav");
            indexHtml.Should().Contain("<main");

            // Internal links rewritten
            indexHtml.Should().NotContain(".md\"");

            // Mermaid diagram present (if quick-start has one)
            var quickStartHtml = await File.ReadAllTextAsync(
                Path.Combine(outputDir, "getting-started", "quick-start.html"));
            quickStartHtml.Should().Contain("class=\"mermaid\"");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task StagedBuild_ParseThenTransform_SameResult()
    {
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "full-site");
        var intermediateDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Stage 1: Parse
            var parseResult = await ParseStage.ExecuteAsync(
                sourceDir, intermediateDir,
                title: "Test", baseUrl: "/");
            parseResult.Success.Should().BeTrue();
            File.Exists(Path.Combine(intermediateDir, "site-manifest.xml"))
                .Should().BeTrue();

            // Stage 2: Transform
            var transformResult = await TransformStage.ExecuteAsync(
                intermediateDir, outputDir);
            transformResult.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "index.html")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(intermediateDir))
                Directory.Delete(intermediateDir, recursive: true);
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run all tests**

```bash
dotnet test Crucible.slnx -v minimal
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add end-to-end integration tests for full and staged builds"
```

---

## Task 16: Final Verification and Cleanup

- [ ] **Step 1: Run full test suite**

```bash
dotnet test Crucible.slnx -v normal
```

Expected: All tests pass, no warnings treated as errors.

- [ ] **Step 2: Build in Release mode**

```bash
dotnet build Crucible.slnx -c Release
```

Expected: Clean build.

- [ ] **Step 3: Run CLI smoke test**

```bash
cd /tmp && mkdir crucible-final-test && cd crucible-final-test
dotnet run --project /raid/elvogel/repos/phoenixmldb/crucible/src/Crucible.Cli -- init
dotnet run --project /raid/elvogel/repos/phoenixmldb/crucible/src/Crucible.Cli -- build --timing
ls -la dist/
cat dist/index.html | head -30
rm -rf /tmp/crucible-final-test
```

Expected: Full pipeline works, timing output shown, HTML output is valid.

- [ ] **Step 4: Verify no compiler warnings**

```bash
dotnet build Crucible.slnx 2>&1 | grep -i "warning" | grep -v "^$" || echo "No warnings"
```

- [ ] **Step 5: Final commit**

```bash
git add -A
git commit -m "chore: final verification and cleanup"
```
