# ndggr — Plan

> .NET Library + CLI für DuckDuckGo-Suche vom Terminal, inspiriert von [jarun/ddgr](https://github.com/jarun/ddgr).
> Projektstruktur nach dem Vorbild von [ladybug-csharp](../ladybug-csharp).

---

## 1. Feature-Analyse von ddgr

`ddgr` ist ein Python-CLI-Tool (einzelne Datei, ~2.800 Zeilen), das DuckDuckGo über die [HTML-Version](https://html.duckduckgo.com/html/) abfragt. Kernfeatures:

| Kategorie | Feature |
|---|---|
| **Suche** | Freitext-Keywords, Custom Ergebnisse pro Seite (0–25), Region-Filter (`-r us-en`), Zeitfilter (Tag/Woche/Monat/Jahr), Site-spezifische Suche (`-w site.com`), Keyword-Operatoren (`filetype:`, `site:`) |
| **DuckDuckGo Bangs** | `!w`, `!yt` etc. — direkt aus CLI oder Omniprompt, GUI-Browser-Option (`--gb`) |
| **Instant Answers** | Direkte Antworten von DDG (`-i / --instant`) |
| **I'm Feeling Ducky** | Erstes Ergebnis direkt im Browser öffnen (`-j / --ducky`) |
| **REPL / Omniprompt** | Interaktiver Modus mit Navigation (next/prev/first), Ergebnis öffnen (Index/Range/alle), neue Suche, URL-Expansion toggle, URL in Clipboard kopieren |
| **Output** | Farbige Terminal-Ausgabe (konfigurierbar, `--colors`), JSON-Ausgabe (`--json`), URL-Expansion (`-x`) |
| **Privacy** | Do Not Track default, User Agent deaktivierbar (`--noua`), HTTPS-Proxy-Support (`-p`), Safe Search toggle (`--unsafe`) |
| **Integration** | Text-Browser-Integration (`BROWSER` env var), Custom URL Handler Script, Clipboard-Support (plattformübergreifend), Shell Completion (Bash/Fish/Zsh) |

---

## 2. Machbarkeitsanalyse

**Ergebnis: Vollständig machbar.**

| Aspekt | Bewertung | Umsetzung in .NET |
|---|---|---|
| DDG HTML scrapen | ✅ Einfach | `HttpClient` + `AngleSharp` (HTML-Parser). DDG HTML-Version liefert stabiles HTML. |
| CLI-Argumente | ✅ Einfach | `System.CommandLine` — vollständige Parity mit ddgr-Optionen möglich |
| REPL / Omniprompt | ✅ Möglich | `System.Console` / `Spectre.Console` für interaktiven Modus |
| Farbige Ausgabe | ✅ Einfach | `Spectre.Console` oder ANSI-Escape-Codes direkt |
| JSON-Output | ✅ Trivial | `System.Text.Json` (built-in) |
| Proxy-Support | ✅ Built-in | `HttpClient` + `HttpClientHandler.Proxy` |
| Clipboard | ✅ Möglich | Plattformspezifisch via Process (wie ddgr: `clip`, `pbcopy`, `xclip`) |
| Browser öffnen | ✅ Einfach | `Process.Start` mit URL |
| Self-contained Binary | ✅ Einfach | `dotnet publish -r <RID> --self-contained -p:PublishSingleFile=true` |
| Cross-Platform | ✅ Einfach | `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64` |

### Vorteile gegenüber ddgr (Python)
- Keine Runtime-Abhängigkeit (self-contained binary)
- Deutlich schnellere Startup-Zeit
- Wiederverwendbare Library (NuGet) für andere .NET-Projekte
- Typsicheres API

---

## 3. Projektstruktur (ladybug-csharp-Stil)

```
ndggr/
├── .github/
│   └── workflows/
│       ├── ci.yml                  # Build + Test Matrix (net8.0, net10.0)
│       ├── pack.yml                # NuGet Pack (manuell)
│       └── release.yml             # Tag-triggered: Test → Pack → Publish NuGet → GitHub Release mit Binaries
├── src/
│   ├── Ndggr/                      # Core Library (NuGet: Ndggr)
│   │   ├── Ndggr.csproj
│   │   ├── DdgClient.cs            # HttpClient-basierter DDG-Zugriff
│   │   ├── DdgSearchOptions.cs     # Region, Time, SafeSearch, Proxy, ...
│   │   ├── SearchResult.cs         # Title, Url, Snippet
│   │   ├── InstantAnswer.cs        # Instant-Answer-Modell
│   │   └── Parsing/
│   │       └── HtmlResultParser.cs  # AngleSharp-basiertes HTML-Parsing
│   ├── Ndggr.Extensions/           # Extensions Library (NuGet: Ndggr.Extensions)
│   │   ├── Ndggr.Extensions.csproj
│   │   ├── DependencyInjection/
│   │   │   └── ServiceCollectionExtensions.cs
│   │   ├── Json/
│   │   │   └── SearchResultJsonExtensions.cs
│   │   └── Formatters/
│   │       └── ConsoleFormatter.cs  # Farbige Terminal-Ausgabe
│   ├── Ndggr.Content/              # URL -> strukturierter Inhalt (NuGet: Ndggr.Content)
│   │   ├── Ndggr.Content.csproj
│   │   ├── ContentFetchClient.cs    # Robustes HTTP-Fetching (Retry, Redirect, Timeout)
│   │   ├── ContentExtractionOptions.cs
│   │   ├── Models/
│   │   │   ├── ContentDocument.cs    # JSON-Schema für LLM-Workflows
│   │   │   └── ContentChunk.cs
│   │   ├── Extraction/
│   │   │   ├── MainContentExtractor.cs
│   │   │   ├── MetadataExtractor.cs
│   │   │   └── LinkExtractor.cs
│   │   ├── Markdown/
│   │   │   └── HtmlToMarkdownConverter.cs
│   │   └── Chunking/
│   │       └── HeadingAwareChunker.cs
│   └── Ndggr.Cli/                  # CLI Tool (NuGet Tool: ndggr)
│       ├── Ndggr.Cli.csproj
│       ├── Program.cs
│       ├── Commands/
│       │   ├── SearchCommand.cs
│       │   └── FetchCommand.cs
│       ├── Repl/
│       │   └── Omniprompt.cs        # Interaktiver REPL-Modus
│       └── Platform/
│           ├── BrowserLauncher.cs
│           └── ClipboardHelper.cs
├── tests/
│   ├── Ndggr.Tests.Unit/           # Unit Tests (HTML-Parsing, Modelle, Formatter)
│   │   └── Ndggr.Tests.Unit.csproj
│   └── Ndggr.Tests.Integration/    # Integration Tests (echte DDG-Abfragen)
│       └── Ndggr.Tests.Integration.csproj
│   ├── Ndggr.Content.Tests.Unit/   # Unit Tests für Extraction/Markdown/Chunking
│   │   └── Ndggr.Content.Tests.Unit.csproj
│   └── Ndggr.Content.Tests.Integration/ # Integration Tests gegen reale Seiten
│       └── Ndggr.Content.Tests.Integration.csproj
├── benchmarks/
│   └── Ndggr.Benchmarks/           # BenchmarkDotNet (Parsing-Performance)
│       └── Ndggr.Benchmarks.csproj
│   └── Ndggr.Content.Benchmarks/   # BenchmarkDotNet (Extraction/Chunking)
│       └── Ndggr.Content.Benchmarks.csproj
├── examples/
│   └── Ndggr.Example/              # Beispiel-App: Library-Nutzung
│       └── Ndggr.Example.csproj
├── scripts/                         # Build-/Release-Hilfsscripte
├── artifacts/
├── Directory.Build.props
├── ndggr.sln
├── .editorconfig
├── .gitignore
├── README.md
├── LICENSE
├── CONTRIBUTING.md
├── SECURITY.md
└── CODEOWNERS
```

---

## 4. Schichtarchitektur

```
┌─────────────────────────────┐
│        Ndggr.Cli            │  CLI-Tool (System.CommandLine)
│  Commands, REPL, Platform   │  → Self-contained Binaries
├─────────────────────────────┤
│       Ndggr.Content         │  URL-Fetch, Main-Content, Markdown, Chunking
│                             │  → NuGet Package
├─────────────────────────────┤
│     Ndggr.Extensions        │  DI, JSON-Export, Console-Formatter
│                             │  → NuGet Package
├─────────────────────────────┤
│          Ndggr              │  Core: DdgClient, Parser, Modelle
│  HttpClient + AngleSharp    │  → NuGet Package
└─────────────────────────────┘
```

- **Ndggr** (lib): Kein Dependency auf Console/CLI. Rein HTTP + Parsing. Targets: `net8.0;net10.0`.
- **Ndggr.Content** (lib): URL-Ingestion, Extraktion, Markdown, Chunking. Targets: `net8.0;net10.0`.
- **Ndggr.Extensions** (lib): Optionale Erweiterungen. Targets: `net8.0;net10.0`.
- **Ndggr.Cli** (tool): Konsolen-App. Target: `net10.0`. Wird als `dotnet tool` + self-contained Binary veröffentlicht.

---

## 5. Dependencies

| Projekt | Package | Zweck |
|---|---|---|
| Ndggr | `AngleSharp` | HTML-Parsing der DDG-Ergebnisse |
| Ndggr.Content | `AngleSharp` | DOM Parsing + robuste Extraktionsbasis |
| Ndggr.Content | `ReverseMarkdown` | HTML -> Markdown (Phase 1: ausschließlich diese Engine) |
| Ndggr.Extensions | `Microsoft.Extensions.DependencyInjection.Abstractions` | DI-Integration |
| Ndggr.Cli | `System.CommandLine` | CLI-Argument-Parsing |
| Ndggr.Cli | `Spectre.Console` | Farbige Ausgabe, REPL |
| Tests | `xUnit`, `FluentAssertions`, `NSubstitute` | Testing |
| Benchmarks | `BenchmarkDotNet` | Performance-Messungen |

### 5.1 NuGet-Entscheidungsmatrix (Transformation)

| Problem | Primär | Fallback | Exit-Kriterium (auf eigene Heuristik umschalten) |
|---|---|---|---|
| HTML parsen | `AngleSharp` | `HtmlAgilityPack` | Wenn Parsing bei >1% der Test-Fixtures fehlschlägt oder DOM inkonsistent ist |
| Main-Content extrahieren | Readability (über aktiv gepflegten .NET-Port) | Eigener Scoring-Extractor auf `AngleSharp` | Wenn Readability in Regression-Suite <90% brauchbaren Hauptinhalt liefert |
| HTML -> Markdown | `ReverseMarkdown` | Kein Fallback in Phase 1 (bewusst) | Nach Testreview entscheiden: Wenn >5% Golden Files unbrauchbar, dann Post-Processing-Regeln ergänzen |
| HTML Sanitizing | `Ganss.Xss` | Strikter eigener Allowlist-Filter | Wenn Sanitizer legitime Inhalte entfernt oder riskante Tags/Attribute durchlässt |
| JSON-Serialisierung | `System.Text.Json` | `Newtonsoft.Json` | Nur wechseln, wenn zwingende Features fehlen (z. B. spezielles Polymorphie-Szenario) |
| URL-Normalisierung | `Uri` + eigene Regeln | `Flurl` URL-Utilities | Wenn Canonicalisierung in Tests nicht deterministisch ist |

**Entscheidungsregel:** Library-first, Pipeline-owned. Das heißt: Wir nutzen stabile NuGet-Libs, aber Retry/Fallback/Schema/Chunking/Fehlerklassen bleiben in eigener Kontrolle.

**Qualitätsgrenzen für Go/No-Go pro Library:**

1. Golden-File Pass Rate >= 95%
2. Keine High-Severity Security Findings im genutzten Package
3. Deterministisches Ergebnis bei identischem Input
4. Kein signifikanter Regression-Effekt im Benchmark-Gate (max Ratio 1.10)

---

## 6. CI/CD Pipeline (GitHub Actions)

### ci.yml — Continuous Integration
- **Trigger**: Push auf `main`, Pull Requests
- **Matrix**: `net8.0`, `net10.0`
- **Steps**: Restore → Build → Test (inkl. Content-Unit+Integration) → Performance Gate (Benchmarks)
- **Artifacts**: Test-Results (.trx)

### pack.yml — NuGet Pack (manuell)
- **Trigger**: `workflow_dispatch`
- Pack aller drei Projekte mit `ContinuousIntegrationBuild=true`, Symbol-Packages

### release.yml — Release
- **Trigger**: Tag `v*` oder `workflow_dispatch` mit Version
- **Steps**:
  1. Build + Test + Performance Gate
  2. NuGet Pack (Ndggr, Ndggr.Extensions)
  3. CLI Publish: Self-contained Binaries für alle Plattformen:
     - `win-x64`, `win-arm64`
     - `linux-x64`, `linux-arm64`
     - `osx-x64`, `osx-arm64`
  4. GitHub Release erstellen (Binaries + NuGet Packages als Assets)
  5. NuGet Push (Ndggr, Ndggr.Extensions, Ndggr.Content)
  6. Optional: `dotnet tool` Push (Ndggr.Cli als Global Tool)

---

## 7. Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Deterministic>true</Deterministic>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

---

## 8. Feature-Roadmap (priorisiert)

### Phase 1 — MVP (Core Search)
1. [ ] Projekt-Scaffolding (Solution, Projects, Directory.Build.props, .editorconfig)
2. [ ] `Ndggr` Core Library: `DdgClient` mit `SearchAsync(query, options)`
3. [ ] HTML-Parser für DDG-Ergebnisseite (Titel, URL, Snippet)
4. [ ] `SearchResult`-Modell + `DdgSearchOptions` (Anzahl Ergebnisse, Region, Zeitfilter)
5. [ ] `Ndggr.Cli`: Basis-Suchbefehl (`ndggr hello world`)
6. [ ] Farbige Terminal-Ausgabe
7. [ ] Unit Tests (Parsing mit gespeicherten HTML-Fixtures)
8. [ ] CI Workflow (ci.yml)

### Phase 2 — Feature Parity
9. [ ] Instant Answers (`-i`)
10. [ ] I'm Feeling Ducky (`-j`) — erstes Ergebnis im Browser öffnen
11. [ ] DuckDuckGo Bangs Support
12. [ ] Site-spezifische Suche (`-w`)
13. [ ] REPL / Omniprompt (interaktiver Modus mit Navigation)
14. [ ] JSON-Output (`--json`)
15. [ ] Proxy-Support (`-p`) in CLI **und** Library-Options
16. [ ] Safe Search Toggle, NoUA, Do Not Track
17. [ ] URL-Expansion (`-x`)
18. [ ] Clipboard-Support (plattformübergreifend)
19. [ ] Integration Tests

### Phase 3 — Polish & Release
20. [ ] `Ndggr.Extensions`: DI-Integration, JSON-Export-Helpers
21. [ ] Benchmarks (HTML-Parsing-Performance)
22. [ ] Self-contained Binary Publish (release.yml)
23. [ ] NuGet-Packaging (pack.yml, release.yml)
24. [ ] `dotnet tool install -g ndggr`
25. [ ] README mit Beispielen + Asciicast
26. [ ] Shell Completion Scripts (Bash, PowerShell, Zsh)
27. [ ] Example-Projekt

### Phase 4 — Content Extraction für LLM Tools
28. [ ] `Ndggr.Content`: Robustes URL-Fetching (Retry, Timeout, Redirect, Backoff, Compression)
29. [ ] Main-Content-Extraktion (Boilerplate/Nav/Footer entfernen)
30. [ ] Metadata-Extraktion (title, canonical, language, author, published date)
31. [ ] HTML -> Markdown Konvertierung via ReverseMarkdown (Phase 1), danach Testreview
32. [ ] JSON-Schema `ContentDocument` + JSONL-Ausgabe
33. [ ] Chunking (heading-aware + size limits) mit stabilen Chunk-IDs
34. [ ] CLI: `ndggr fetch <url>` und Batch-Mode `--input urls.txt`
35. [ ] Fehlerklassen + Exit Codes + strukturierte Error-JSON
36. [ ] Golden-File Tests mit realen HTML-Fixtures
37. [ ] Integrations-Tests gegen News, Docs, Blogs, GitHub, Wikipedia
38. [ ] Content-Benchmarks + Performance-Gate
39. [ ] Library-DX: Simple Facade API (One-liner) + Advanced Options API
40. [ ] Proxy-End-to-End Tests (Search + Fetch) inkl. Auth-Proxy-Szenarien

---

## 9. CLI Interface Design

```
Usage: ndggr <command> [options]

Commands:
  search                DuckDuckGo Suche
  fetch                 URL in Markdown oder JSON umwandeln

Search Usage:
  ndggr search [options] [keywords...]

Arguments:
  keywords              Search keywords

Options:
  -n, --num <N>         Results per page (0-25, default 10)
  -r, --region <REG>    Region (e.g. de-de, us-en)
  -t, --time <SPAN>     Time filter: d (day), w (week), m (month), y (year)
  -w, --site <SITE>     Site-specific search
  -i, --instant         Show only instant answer
  -j, --ducky           Open first result in browser
  -x, --expand          Show full URLs
  -p, --proxy <URI>     HTTPS proxy
  --unsafe              Disable safe search
  --noua                Disable user agent
  --json                JSON output (implies --noprompt)
  --noprompt            Search and exit, no REPL
  --colors <COLORS>     Color configuration string
  --colorize <MODE>     auto|always|never
  --url-handler <UTIL>  Custom URL handler
  -v, --version         Show version
  -d, --debug           Enable debug logging

REPL Keys:
  n / p / f             Next / Previous / First page
  <index>               Open result in browser
  o <index|range|a>     Open result(s) in browser
  d <keywords>          New search
  x                     Toggle URL expansion
  c <index>             Copy URL to clipboard
  q / Ctrl+D            Exit
```

```bash
Fetch Usage:
  ndggr fetch <url> [options]

Fetch Options:
  --format <FORMAT>         markdown|json|jsonl (default: markdown)
  --main-content-only       Boilerplate soweit möglich entfernen (default: true)
  --include-links           Extrahierte Links im Output aufnehmen
  --chunk-size <N>          Zielgröße pro Chunk (Zeichen)
  --max-chunks <N>          Obergrenze für Chunk-Anzahl
  --timeout-ms <N>          HTTP Timeout pro Request
  --max-retries <N>         Retry-Anzahl bei transient errors
  --proxy <URI>             HTTPS Proxy für Fetch-Requests
  --user-agent <UA>         Eigener User-Agent
  --header <K:V>            Zusätzliche Header (mehrfach)
  --json-schema-version <V> Output-Schema-Version (default: 1)
  --output <PATH>           Ausgabe in Datei statt stdout
  --pretty                  JSON eingerückt ausgeben
```

---

## 10. URL -> JSON/Markdown: Robuste Konvertierung

### 10.1 Konvertierungs-Pipeline

1. **Validate**
   - URL validieren (Schema http/https, max Länge, kein lokaler/gefährlicher Host wenn gewünscht)
   - Normalisieren (Canonical URL bilden, Tracking-Parameter optional entfernen)

2. **Fetch**
   - `HttpClient` mit konfigurierbarem Timeout
   - Redirect-Handling (max Redirects, Schleifen erkennen)
    - Retry mit exponential backoff + jitter bei 408/429/5xx
    - Optional Proxy + Custom Header + User-Agent (via Library Options + CLI Flags)
   - Content-Encoding support (gzip/br/deflate)

3. **Decode & Parse**
   - Charset robust bestimmen (Header, meta, fallback UTF-8)
   - HTML in DOM parsen
   - Bei Nicht-HTML: text/*, application/json, application/xml getrennt behandeln

4. **Extract Main Content**
   - Entferne script/style/nav/footer/aside/ads
   - Kandidatenblöcke scoren (Textdichte, Linkdichte, Überschriftennähe)
   - Fallback auf `<article>`, `<main>`, sonst body-Heuristik

5. **Extract Metadata**
   - `title`, `meta description`, `og:title`, `og:description`
   - `canonical`, `lang`, `author`, `published_time`, `modified_time`
   - `h1..h6` Struktur, Outbound/Internal Links

6. **Transform**
   - Plain text normalisieren (Whitespace, Zeilenumbrüche)
   - Markdown erzeugen (Listen, Tabellen, Codeblöcke erhalten)
   - Optional sanitizing (tracking query params entfernen)

7. **Chunk**
   - Heading-aware chunking
   - Hard limit pro Chunk (z. B. 2k-4k chars)
   - Jeder Chunk erhält stabile ID + section path + source URL

8. **Emit**
   - Markdown oder JSON/JSONL
   - Konsistente Schema-Version
   - Bei Fehlern strukturierte Fehlerantwort mit Diagnosefeldern

### 10.2 JSON-Schema (v1)

```json
{
  "schemaVersion": 1,
  "sourceUrl": "https://example.com/post",
  "resolvedUrl": "https://www.example.com/post",
  "fetchedAtUtc": "2026-03-30T12:34:56Z",
  "statusCode": 200,
  "contentType": "text/html; charset=utf-8",
  "title": "Example Post",
  "description": "...",
  "language": "en",
  "author": "...",
  "publishedAt": "2026-01-01T00:00:00Z",
  "canonicalUrl": "https://example.com/post",
  "headings": [
    { "level": 1, "text": "Title", "anchor": "title" }
  ],
  "links": [
    { "url": "https://...", "text": "...", "rel": "nofollow", "isInternal": false }
  ],
  "text": "Normalized plain text...",
  "markdown": "# Title\n\n...",
  "chunks": [
    {
      "id": "sha256:...",
      "index": 0,
      "section": "Title > Section A",
      "text": "...",
      "startChar": 0,
      "endChar": 1800,
      "sourceUrl": "https://example.com/post"
    }
  ],
  "diagnostics": {
    "extractor": "heuristic-v1",
    "fetchRetries": 1,
    "warnings": ["low-text-density"]
  }
}
```

### 10.3 Markdown-Ausgabe

- Default: Nur Hauptinhalt als Markdown
- Optional Frontmatter:

```yaml
---
source_url: https://example.com/post
resolved_url: https://www.example.com/post
title: Example Post
language: en
fetched_at_utc: 2026-03-30T12:34:56Z
---
```

- Danach Markdown-Body mit stabilen Überschriften und bereinigten Links

### 10.4 Robustheit pro Funktion

| Funktion | Robustheitsanforderung |
|---|---|
| URL-Validierung | Rejected host schemes, Längenlimit, klare Fehlermeldung |
| HTTP-Fetch | Timeout, Retry, Redirect-Limit, Rate-limit-respektierender Backoff |
| Decoder | Charset fallback-Kette, Binary-Detection |
| Extractor | Mehrstufige Heuristik + Fallback auf body-text |
| Markdown Converter | Erhält Listen/Code/Tabellen, entfernt unsichere HTML-Fragmente |
| Chunker | Deterministisches Chunking bei identischem Input |
| Serializer | Schema-versioniert, kompatibel, null-safe |
| CLI Output | Exit Codes + stdout/stderr Trennung + optional Error-JSON |

### 10.5 Teststrategie für maximale Robustheit

1. Unit Tests je Pipeline-Stufe (validator/fetcher/extractor/converter/chunker)
2. Golden-File Tests: HTML Fixture -> erwartetes JSON + Markdown
3. Property-based Tests für Normalisierung/Chunking (stabil, keine Endlosschleifen)
4. Integration Tests mit kontrollierten Testseiten (verschiedene Sprachen/Layouts)
5. Failure Injection Tests: Timeout, 429, kaputte HTML-Strukturen, Redirect-Loops
6. Regression-Suite gegen historisch problematische URLs

### 10.6 LLM-Tooling Schnittstelle

Geplante Tool-Funktionen auf Library-Ebene:

- `SearchAsync(query, options)`
- `FetchAsync(url, options)`
- `FetchBatchAsync(urls, options)`
- `ExtractChunksAsync(url, options)`

Damit kann später direkt ein MCP-Server oder ein anderes Tool-Protocol angebunden werden.

### 10.7 Library API Einfachheit (CLI-ähnliche DX)

Ziel: Die Library muss so leicht nutzbar sein wie die CLI.

Public API-Design:

1. **Simple Facade (empfohlen für 80% der Fälle)**
   - `NdggrClient.SearchAsync("hello world")`
   - `NdggrClient.FetchMarkdownAsync("https://example.com")`
   - `NdggrClient.FetchJsonAsync("https://example.com")`

2. **Advanced API (für volle Kontrolle)**
   - `SearchAsync(query, SearchOptions)`
   - `FetchAsync(url, FetchOptions)`
   - Optionen enthalten explizit Proxy/Timeout/Retry/Header/UserAgent

3. **DI-Friendly Factory**
   - `services.AddNdggr(...)`
   - Konfiguration zentral über `NdggrClientOptions`

### 10.8 Proxy in der Library (verbindlich)

Proxy-Support ist verpflichtend in allen relevanten Optionstypen:

- `SearchOptions.ProxyUri`
- `FetchOptions.ProxyUri`
- `NdggrClientOptions.ProxyUri` (global default)

Priorität der Konfiguration:

1. Pro Request Option (`SearchOptions`/`FetchOptions`)
2. Client Default (`NdggrClientOptions`)
3. Environment (`HTTPS_PROXY`/`https_proxy`) optional als letzter Fallback

Akzeptierte Proxy-Formate:

- `http://host:port`
- `http://user:pass@host:port`
- `https://host:port`

Fehlerverhalten:

- Ungültige Proxy-URI -> klare `ArgumentException` (vor Request)
- Proxy-Connect-Fehler -> domänenspezifische Exception (z. B. `NdggrNetworkException`)
- 407 (Proxy Authentication Required) -> spezifischer Fehlercode/Message

### 10.9 API-Beispiele für Einfachheit

```csharp
// Simple usage
var client = NdggrClient.Create();
var results = await client.SearchAsync("open source cli");
var md = await client.FetchMarkdownAsync("https://example.com");
```

```csharp
// Simple usage with proxy
var client = NdggrClient.Create(new NdggrClientOptions
{
  ProxyUri = new Uri("http://127.0.0.1:8080")
});
var doc = await client.FetchJsonAsync("https://example.com");
```

```csharp
// Advanced per-request override
var doc = await client.FetchAsync(
  "https://example.com",
  new FetchOptions
  {
    ProxyUri = new Uri("http://user:pass@proxy.local:8080"),
    Timeout = TimeSpan.FromSeconds(20),
    MaxRetries = 3
  });
```

---

## 11. README-Positionierung (ndggr)

README soll klar kommunizieren:

1. **ndggr ist eine Weiterentwicklung von ddgr** mit .NET-Implementierung.
2. **Kein Fork des Python-Codes**, sondern eigene Implementierung inspiriert von ddgr-Features.
3. **Zielgruppe:** CLI-User plus .NET-Entwickler, die eine Library/API brauchen.

Pflichtinhalte in README:

1. Projektbeschreibung: "Weiterentwicklung von ddgr in .NET"
2. Vorteile:
  - Self-contained Single-File Binaries (keine Runtime-Installation nötig)
  - Cross-Platform Releases (Windows, Linux, macOS; x64/arm64)
  - Wiederverwendbare Libraries (`Ndggr`, `Ndggr.Content`, `Ndggr.Extensions`)
  - URL -> Markdown/JSON/JSONL Fetch-Pipeline für LLM-Workflows
  - Robuste Fehlerbehandlung (Retry, Timeout, strukturierte Fehlerausgaben)
3. Quickstart:
  - Suche: `ndggr search hello world`
  - Fetch Markdown: `ndggr fetch https://example.com --format markdown`
  - Fetch JSON: `ndggr fetch https://example.com --format json --pretty`
4. Proxy-Beispiele:
  - Search: `ndggr search --proxy http://127.0.0.1:8080 test`
  - Fetch: `ndggr fetch https://example.com --proxy http://127.0.0.1:8080`
5. Architektur-Überblick + Link auf Benchmarks/Tests/Release-Artefakte
6. Library-Quickstart mit 3 kurzen Snippets:
  - One-liner Search
  - One-liner FetchMarkdown
  - Proxy-Konfiguration global + per Request

---

## 12. Festgelegte Entscheidungen

---

| # | Frage | Optionen | Empfehlung |
|---|---|---|---|
| 1 | Projektname / NuGet-ID | `Ndggr`, `DuckSearch`, `DdgNet` | `Ndggr` |
| 2 | HTML-Parser | `AngleSharp` vs `HtmlAgilityPack` | `AngleSharp` (moderner, async-native, bessere CSS-Selektoren) |
| 3 | CLI-Framework | `System.CommandLine` vs `Spectre.Console.Cli` | `System.CommandLine` (offizielles MS-Paket, leichter) |
| 4 | REPL-Rendering | `Spectre.Console` vs raw ANSI | `Spectre.Console` (Markup, Farben, Cross-Platform) |
| 5 | Min. .NET Version | `net8.0` LTS only vs `net8.0;net10.0` | Dual-Target für Libraries, `net10.0` für CLI |
| 6 | Lizenz | MIT vs GPL-3.0 (wie ddgr) | MIT (eigene Implementierung, kein Code von ddgr) |
| 7 | Markdown Engine | `ReverseMarkdown` vs eigener Converter | Festgelegt: Phase 1 nur `ReverseMarkdown`, danach Entscheidung per Testreview |
| 8 | Main Content Extraction | externe Library vs eigene Heuristik | Festgelegt: Readability (aktiv gepflegter .NET-Port) als Primary, Fallback auf eigene AngleSharp-Heuristik bei <90% Quality Gate |
| 9 | JSON-Schema-Strategie | lose Felder vs versioniertes Schema | Festgelegt: Versioniertes Schema (`schemaVersion`), additive Felder in v1 erlaubt, keine Breaking Changes ohne v2 |
