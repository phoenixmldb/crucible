# Crucible

Static documentation generator that transforms Markdown into HTML through an XML intermediate representation and XSLT 4.0 transformation.

## Packages

| Package | Description | Install |
|---------|-------------|---------|
| **Crucible.Cli** | Command-line tool | `dotnet tool install -g Crucible.Cli` |
| **Crucible.Extensions** | Extension library (Mermaid diagrams, plugin API) | Library — reference in your project |

## Quick start

```bash
# Install the CLI
dotnet tool install -g Crucible.Cli

# Create a new docs project
crucible init

# Build the site
crucible build
```

## Features

- **Markdown in, HTML out** — write docs in Markdown with YAML frontmatter
- **XSLT-powered** — transforms via the PhoenixmlDb XSLT 4.0 engine
- **Client-side search** — Lunr.js with pre-built index
- **Dark mode** — automatic with manual toggle
- **SEO** — Open Graph, canonical links, sitemap.xml
- **Extensible** — plugin model for custom Markdown processing
- **LLMs.txt** — auto-generated site overview for AI consumption

## Documentation

Full documentation at [phoenixml.dev](https://phoenixml.dev)

## License

Apache 2.0
