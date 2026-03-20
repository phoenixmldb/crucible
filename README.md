# Crucible

Static documentation generator that transforms Markdown into HTML through an XML intermediate representation and XSLT 4.0 transformation. Powered by the [PhoenixmlDb](https://github.com/phoenixmldb) XSLT engine.

## Install

```bash
dotnet tool install -g Crucible.Cli
```

## Quick start

```bash
crucible init      # creates crucible.yaml + docs/index.md
crucible build     # generates static site in dist/
```

## Features

- **Markdown in, HTML out** — write docs in Markdown with YAML frontmatter
- **XSLT-powered** — transforms via the PhoenixmlDb XSLT 4.0 engine
- **Client-side search** — Lunr.js with pre-built index
- **Dark mode** — automatic with manual toggle
- **SEO** — Open Graph, canonical links, sitemap.xml, llms.txt
- **Extensible** — plugin model for custom Markdown processing
- **Mermaid diagrams** — client-side rendering via built-in extension

## Packages

| Package | Description |
|---------|-------------|
| [Crucible.Cli](https://www.nuget.org/packages/Crucible.Cli) | Command-line dotnet tool |
| [Crucible.Extensions](https://www.nuget.org/packages/Crucible.Extensions) | Extension library and plugin API |

## Documentation

Full documentation at [phoenixml.dev](https://phoenixml.dev)

## License

Apache 2.0
