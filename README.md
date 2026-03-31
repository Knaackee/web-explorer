# ndggr

**DuckDuckGo search & content extraction for .NET** — CLI tool and library.

A modern .NET reimagining of [ddgr](https://github.com/jarun/ddgr), with added content fetching, Markdown conversion, and JSON output for LLM workflows.

## Features

- **Web Search** — DuckDuckGo HTML search with region, time filters, safe search, and pagination
- **Content Fetch** — Extract main content from any URL using the Readability algorithm
- **Markdown Output** — Clean Markdown from web pages, optimized for LLM consumption
- **JSON/JSONL** — Structured `ContentDocument` output with schema versioning
- **Proxy Support** — Explicit `--proxy` or `HTTPS_PROXY` environment variable
- **Privacy First** — No tracking, optional user-agent suppression, DNT headers
- **Single-File Binaries** — Self-contained executables for Windows, Linux, macOS (x64 + ARM64)
- **Library + DI** — Use as NuGet packages with `services.AddNdggr()` integration

## Installation

### CLI (Single-File Binary)

Download from [GitHub Releases](https://github.com/ndggr/ndggr/releases):

| Platform       | Binary                 |
|----------------|------------------------|
| Windows x64    | `ndggr-win-x64.exe`   |
| Windows ARM64  | `ndggr-win-arm64.exe` |
| Linux x64      | `ndggr-linux-x64`     |
| Linux ARM64    | `ndggr-linux-arm64`   |
| macOS x64      | `ndggr-osx-x64`       |
| macOS ARM64    | `ndggr-osx-arm64`     |

### CLI (.NET Tool)

```bash
dotnet tool install -g ndggr
```

### Library (NuGet)

```bash
# Core search library
dotnet add package Ndggr

# Content extraction
dotnet add package Ndggr.Content

# DI + facade (includes both)
dotnet add package Ndggr.Extensions
```

## CLI Usage

### Search

```bash
# Basic search
ndggr search "rust programming language"

# Limit results, filter by region and time
ndggr search "dotnet 10" -n 5 -r de-de -t month

# JSON output
ndggr search "async await best practices" --json

# Instant answer only
ndggr search "weather berlin" -i

# Open first result in browser (ducky mode)
ndggr search "github ndggr" -j

# Through a proxy
ndggr search "privacy tools" -p http://127.0.0.1:8080

# Suppress user-agent, disable safe search
ndggr search "something" --noua --unsafe
```

### Fetch (Content Extraction)

```bash
# Extract as Markdown (default)
ndggr fetch https://example.com/article

# JSON output with full metadata
ndggr fetch https://example.com/article --format json --pretty

# JSONL (one document per line)
ndggr fetch https://example.com/article --format jsonl

# With chunking for LLM context windows
ndggr fetch https://example.com/article --format json --chunk-size 2000 --max-chunks 10

# Include extracted links
ndggr fetch https://example.com/article --format json --include-links

# Save to file
ndggr fetch https://example.com/article --output article.md

# Through a proxy
ndggr fetch https://example.com/article -p http://127.0.0.1:8080
```

## Library Usage

### Quick Start (Facade API)

```csharp
using Ndggr.Extensions;

using var client = new NdggrClient();

// Search
var results = await client.SearchAsync("dotnet performance");
foreach (var r in results.Results)
    Console.WriteLine($"{r.Title} — {r.Url}");

// Fetch content as Markdown
var markdown = await client.FetchMarkdownAsync("https://example.com/article");
Console.WriteLine(markdown);

// Fetch structured content
var doc = await client.FetchAsync("https://example.com/article");
Console.WriteLine($"Title: {doc.Title}");
Console.WriteLine($"Words: {doc.WordCount}");
Console.WriteLine(doc.Markdown);
```

### Advanced (Options)

```csharp
using Ndggr;
using Ndggr.Content;

// Search with options
var searchOptions = new DdgSearchOptions
{
    Region = "de-de",
    TimeRange = "month",
    MaxResults = 10
};
using var ddg = new DdgClient();
var response = await ddg.SearchAsync("query", searchOptions);

// Content extraction with options
using var pipeline = new ContentPipeline();
var extractionOptions = new ContentExtractionOptions
{
    ChunkSize = 2000,
    MaxChunks = 10,
    IncludeLinks = true,
    Proxy = new Uri("http://127.0.0.1:8080")
};
var doc = await pipeline.ProcessAsync("https://example.com/article", extractionOptions);
```

### Dependency Injection

```csharp
using Ndggr.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNdggr(search =>
{
    search.Region = "de-de";
    search.SafeSearch = false;
}, content =>
{
    content.ChunkSize = 2000;
    content.MaxRetries = 3;
});

// Inject NdggrClient anywhere
app.MapGet("/search", async (NdggrClient client, string q) =>
    await client.SearchAsync(q));
```

## ContentDocument JSON Schema (v1)

```json
{
  "schemaVersion": 1,
  "url": "https://example.com/article",
  "title": "Article Title",
  "author": "Author Name",
  "publishedDate": "2025-01-15",
  "language": "en",
  "siteName": "Example",
  "excerpt": "Brief excerpt...",
  "markdown": "# Article Title\n\nContent...",
  "textContent": "Plain text content...",
  "wordCount": 1234,
  "chunks": [
    {
      "id": "a1b2c3d4e5f6",
      "index": 0,
      "heading": "Section Title",
      "content": "Chunk content...",
      "charCount": 1500
    }
  ],
  "links": [
    { "text": "Link Text", "href": "https://example.com/link" }
  ],
  "fetchedAt": "2025-01-15T12:00:00+00:00"
}
```

## Project Structure

```
ndggr.sln
├── src/
│   ├── Ndggr/                  # Core search library (DdgClient, HtmlResultParser)
│   ├── Ndggr.Content/          # Content extraction (Readability, Markdown, Chunking)
│   ├── Ndggr.Extensions/       # Facade + DI integration (NdggrClient)
│   └── Ndggr.Cli/              # CLI tool (search, fetch commands)
├── tests/
│   └── Ndggr.Tests.Unit/       # 204 unit tests
├── benchmarks/
│   └── Ndggr.Benchmarks/       # BenchmarkDotNet performance tests
└── .github/workflows/
    ├── ci.yml                  # CI pipeline (ubuntu/windows × net8.0/net10.0)
    └── release.yml             # Release pipeline (NuGet + single-file binaries)
```

## Environment Variables

| Variable       | Description              | Example                    |
|----------------|--------------------------|----------------------------|
| `HTTPS_PROXY`  | HTTPS proxy fallback     | `http://127.0.0.1:8080`   |
| `https_proxy`  | HTTPS proxy fallback     | `http://127.0.0.1:8080`   |

## Requirements

- **.NET 8.0+** for library usage
- **.NET 10.0** for CLI (pre-built binaries require no SDK)

## License

[MIT](LICENSE)

## Credits

- [ddgr](https://github.com/jarun/ddgr) — Original Python CLI that inspired this project
- [primp.net](https://github.com/Knaackee/primp.net) — .NET bridge for browser TLS/HTTP2 impersonation (avoids CAPTCHA detection)
- [primp](https://github.com/deedy5/primp) by deedy5 — The underlying Rust HTTP client with browser fingerprinting
