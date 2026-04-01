# web-explorer

**DuckDuckGo search & content extraction for .NET** — CLI tool and library.

A modern .NET reimagining of [ddgr](https://github.com/jarun/ddgr), with added content fetching, Markdown conversion, and JSON output for LLM workflows.

## Features

- **Web Search** — DuckDuckGo HTML search with region, time filters, safe search, and pagination
- **Content Fetch** — Extract main content from any URL using the Readability algorithm
- **Playwright Sessions** — Start durable browser sessions and reuse them across CLI invocations or library calls
- **Markdown Output** — Clean Markdown from web pages, optimized for LLM consumption
- **Go Markdown Engine (Embedded)** — Bundles the `html-to-markdown` Go binary per platform and runs it from a temporary runtime extraction (no separate install)
- **JSON/JSONL** — Structured `ContentDocument` output with schema versioning
- **Proxy Support** — Explicit `--proxy` or `HTTPS_PROXY` environment variable
- **Privacy First** — No tracking, optional user-agent suppression, DNT headers
- **Single-File Binaries** — Self-contained executables for Windows, Linux, macOS (x64 + ARM64)
- **Library + DI** — Use as NuGet packages with `services.AddWebExplorer()` integration

## Installation

### CLI (One-Line Install)

Windows (PowerShell):

```powershell
Invoke-WebRequest -Uri https://github.com/Knaackee/web-explorer/releases/latest/download/wxp-win-x64.exe -OutFile wxp.exe; Move-Item wxp.exe "$env:LOCALAPPDATA\Microsoft\WindowsApps\wxp.exe" -Force
```

Linux:

```bash
curl -Lo wxp https://github.com/Knaackee/web-explorer/releases/latest/download/wxp-linux-x64 && chmod +x wxp && sudo mv wxp /usr/local/bin/
```

macOS (Apple Silicon):

```bash
curl -Lo wxp https://github.com/Knaackee/web-explorer/releases/latest/download/wxp-osx-arm64 && chmod +x wxp && sudo mv wxp /usr/local/bin/
```

### CLI (Single-File Binary)

Download from [GitHub Releases](https://github.com/Knaackee/web-explorer/releases):

| Platform       | Binary                 |
|----------------|------------------------|
| Windows x64    | `wxp-win-x64.exe`   |
| Windows ARM64  | `wxp-win-arm64.exe` |
| Linux x64      | `wxp-linux-x64`     |
| Linux ARM64    | `wxp-linux-arm64`   |
| macOS x64      | `wxp-osx-x64`       |
| macOS ARM64    | `wxp-osx-arm64`     |

### CLI (.NET Tool)

```bash
dotnet tool install -g WebExplorer.Cli
```

### Playwright Browser Install

Chromium is installed automatically on first use when you run `start-session`, `fetch --session`, or `fetch --renderer playwright`.

If you prefer a manual install ahead of time, you can still run:

```powershell
.\src\WebExplorer.Cli\bin\Release\net10.0\win-x64\playwright.ps1 install chromium
```

### Library (NuGet)

```bash
# Core search library
dotnet add package WebExplorer

# Content extraction
dotnet add package WebExplorer.Content

# DI + facade (includes both)
dotnet add package WebExplorer.Extensions

# Playwright-backed browser sessions
dotnet add package WebExplorer.Playwright
```

## CLI Usage

```bash
# Show global help
wxp help

# Show help for a specific command
wxp help search
wxp help fetch
wxp help start-session
```

### Search

```bash
# Basic search
wxp search "rust programming language"

# Limit results, filter by region and time
wxp search "dotnet 10" -n 5 -r de-de -t month

# JSON output
wxp search "async await best practices" --json

# Instant answer only
wxp search "weather berlin" -i

# Open first result in browser (ducky mode)
wxp search "github web-explorer" -j

# Through a proxy
wxp search "privacy tools" -p http://127.0.0.1:8080

# Suppress user-agent, disable safe search
wxp search "something" --noua --unsafe
```

### Fetch (Content Extraction)

```bash
# Extract as Markdown (default)
wxp fetch https://example.com/article

# JSON output with full metadata
wxp fetch https://example.com/article --format json --pretty

# JSONL (one document per line)
wxp fetch https://example.com/article --format jsonl

# With chunking for LLM context windows
wxp fetch https://example.com/article --format json --chunk-size 2000 --max-chunks 10

# Include extracted links
wxp fetch https://example.com/article --format json --include-links

# Save to file
wxp fetch https://example.com/article --output article.md

# Through a proxy
wxp fetch https://example.com/article -p http://127.0.0.1:8080

# One-off Playwright render
wxp fetch https://example.com/article --renderer playwright

# Fetch through an existing Playwright session
# `--session` automatically switches the renderer to Playwright
wxp fetch https://example.com/article --session my-login
```

### Playwright Sessions

```bash
# Start a headless browser session
wxp start-session --session my-login

# Inspect or list sessions
wxp inspect-session my-login --json
wxp list-sessions --json

# Reuse the same browser profile and cookies across fetches
wxp fetch https://example.com/login --session my-login
wxp fetch https://example.com/account --session my-login --format json

# End the browser session
wxp end-session my-login
```

## Library Usage

### Quick Start (Facade API)

```csharp
using WebExplorer.Extensions;
using WebExplorer.Playwright;

using var client = new WebExplorerClient();

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

// Start a reusable Playwright session
var session = await client.StartPlaywrightSessionAsync(new PlaywrightSessionStartOptions
{
  SessionId = "my-login"
});

var sessionDoc = await client.FetchWithPlaywrightSessionAsync(
  session.SessionId,
  "https://example.com/account");
Console.WriteLine(sessionDoc.Markdown);

await client.EndPlaywrightSessionAsync(session.SessionId);
```

### Advanced (Options)

```csharp
using WebExplorer;
using WebExplorer.Content;
using WebExplorer.Playwright;

// Search with options
var searchOptions = new SearchOptions
{
    Region = "de-de",
    TimeRange = "month",
    MaxResults = 10
};
using var ddg = new SearchClient();
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

// Reusable browser session with Playwright
using var facade = new WebExplorerClient();
var session = await facade.StartPlaywrightSessionAsync(new PlaywrightSessionStartOptions
{
  SessionId = "checkout",
  IdleTimeoutSeconds = 1800,
});

var browserDoc = await facade.FetchWithPlaywrightSessionAsync(
  session.SessionId,
  "https://example.com/cart",
  extractionOptions,
  new PlaywrightNavigationOptions
  {
    TimeoutMs = 45000,
    WaitUntil = PlaywrightWaitUntil.NetworkIdle,
  });

await facade.EndPlaywrightSessionAsync(session.SessionId);
```

### Dependency Injection

```csharp
using WebExplorer.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWebExplorer(search =>
{
    search.Region = "de-de";
    search.SafeSearch = false;
}, content =>
{
    content.ChunkSize = 2000;
    content.MaxRetries = 3;
});

// Inject WebExplorerClient anywhere
app.MapGet("/search", async (WebExplorerClient client, string q) =>
    await client.SearchAsync(q));

app.MapGet("/session", async (WebExplorerClient client) =>
  await client.StartPlaywrightSessionAsync());
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
web-explorer.sln
├── src/
│   ├── WebExplorer/                  # Core search library (SearchClient, HtmlResultParser)
│   ├── WebExplorer.Content/          # Content extraction (Readability, Markdown, Chunking)
│   ├── WebExplorer.Playwright/       # Durable Playwright browser sessions
│   ├── WebExplorer.Extensions/       # Facade + DI integration (WebExplorerClient)
│   └── WebExplorer.Cli/              # CLI tool (search, fetch commands)
├── tests/
│   └── WebExplorer.Tests.Unit/       # 204 unit tests
├── benchmarks/
│   └── WebExplorer.Benchmarks/       # BenchmarkDotNet performance tests
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
- [html-to-markdown](https://github.com/JohannesKaufmann/html-to-markdown) — Go library/CLI embedded for high-quality HTML to Markdown conversion
