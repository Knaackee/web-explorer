# ndggr - Implementation Plan

Dieser Plan ist der operative Umsetzungsplan zu `plan.md` und priorisiert schnelle Lieferbarkeit, robuste Qualität und saubere Releases.

## Leitprinzipien

1. Library und CLI sollen gleich einfach nutzbar sein.
2. ReverseMarkdown bleibt in Phase 1 der einzige Markdown-Converter.
3. Proxy muss durchgaengig funktionieren: CLI und Library.
4. Readability ist Primary fuer Main-Content-Extraction.
5. Jede Phase endet mit klaren Abnahmekriterien.

## Phase 0 - Setup und Entscheidungen fixieren ✅

### Ziel
Projektbasis, Namensraum und Build-Standards final aufsetzen.

### Tasks
1. ✅ Solution und Projektnamen finalisieren (`ndggr.sln`, `Ndggr.*`).
2. ✅ Repo-Struktur gemaess Zielbild aus `plan.md` anlegen.
3. ✅ `Directory.Build.props` und `.editorconfig` konfigurieren.
4. ✅ Baseline CI (`restore`, `build`, `test`) erstellen.
5. ✅ Lizenzdatei und Package-Metadaten (MIT) vorbereiten.

### Status
Abgeschlossen. 5 Projekte im Solution, CI Workflow mit Matrix (ubuntu/windows × net8.0/net10.0).

## Phase 1 - Core Search (Ndggr + Ndggr.Cli) ✅

### Ziel
DuckDuckGo Suche (HTML) als robuste Library und einfache CLI bereitstellen.

### Tasks
1. ✅ `DdgClient` mit `SearchAsync(query, SearchOptions)` implementieren (POST an `html.duckduckgo.com/html/`).
2. ✅ HTML Parsing mit AngleSharp fuer Trefferliste (`HtmlResultParser`).
3. ✅ Modelle: `SearchResult`, `InstantAnswer`, `DdgSearchOptions`, `DdgSearchResponse`.
4. ✅ CLI `ndggr search` mit Basisoptionen (`--num`, `--region`, `--time`, `--site`, `--json`, `--expand`).
5. ✅ Konsolenausgabe in Spectre.Console formatieren (`ResultFormatter`).
6. ✅ 29 Unit Tests (Parser, URL-Extraction, Options, Client mit Mock-Handler).
7. ✅ 5 HTML Fixtures (basic, instant answer, empty, pagination, edge cases).

### Status
Abgeschlossen. Build 0 Warnings, 0 Errors. 29/29 Tests gruen auf net8.0 und net10.0.

## Phase 2 - Proxy, Privacy und Feature Parity (Search) ✅

### Ziel
Feature-Parity fuer Suchbereich inkl. Proxy, Privacy und Spezialsuchen.

### Erledigte Tasks
1. ✅ CLI-Optionen: `--proxy`/`-p`, `--unsafe`, `--noua`, `-i`/`--instant`, `-j`/`--ducky`.
2. ✅ Proxy-Prioritaet: Explicit → `HTTPS_PROXY` / `https_proxy` Env-Var Fallback (`ProxyResolver`).
3. ✅ Instant Answer Modus (`-i`): Nur Instant Answer anzeigen.
4. ✅ Ducky Modus (`-j`): Erstes Ergebnis im Browser oeffnen (plattformuebergreifend).
5. ✅ Fehlerklassen: `NdggrException` → `SearchException` → `RateLimitException`.
6. ✅ DdgClient: HTTP-Fehler und CAPTCHA-Erkennung mit strukturierten Exceptions.
7. ✅ `--json`, `-x`/`--expand`, `-w`/`--site` (bereits aus Phase 1).
8. ✅ `SafeSearch` und `SendUserAgent` in Options (bereits aus Phase 1).
9. ✅ 54 Tests gesamt (25 neue), alle gruen auf net8.0 und net10.0.

### Status
Abgeschlossen. 0 Warnings, 0 Errors. 54/54 Tests gruen.

## Phase 3 - Content Fetch MVP (Ndggr.Content) ✅

### Ziel
URL in strukturierten Inhalt umwandeln: Markdown und JSON.

### Erledigte Tasks
1. ✅ `FetchAsync(url, FetchOptions)` implementieren.
2. ✅ Robustes HTTP-Fetching: Timeout, Retry, Redirect, Compression.
3. ✅ Main-Content Extraction mit Readability (Primary).
4. ✅ HTML -> Markdown mit ReverseMarkdown (nur diese Engine in Phase 1).
5. ✅ JSON-Ausgabe (`ContentDocument`) mit `schemaVersion=1`.
6. ✅ CLI `ndggr fetch <url>` mit `--format markdown|json|jsonl`.
7. ✅ 49 neue Tests (103 gesamt), alle gruen auf net8.0 und net10.0.

### Deliverables
1. End-to-End Fetch Pipeline mit Markdown und JSON Output.
2. Erste Golden-File Testbasis.

### DoD
1. 20+ reale URL-Fixtures laufen stabil durch.
2. Golden-File Pass Rate >= 95% fuer v1-Fixture-Set.
3. Bei Fehlern strukturierte Error-Response mit Diagnosefeldern.

## Phase 4 - Library DX und API-Ergonomie ✅

### Ziel
API soll fuer Nutzer so einfach sein wie CLI.

### Erledigte Tasks
1. ✅ Simple Facade API: `SearchAsync("...")`, `FetchMarkdownAsync("...")`, `FetchAsync("...")`.
2. ✅ Advanced API mit `DdgSearchOptions` und `ContentExtractionOptions`.
3. ✅ DI Integration (`services.AddNdggr(...)`).
4. ✅ Konsistente Defaults fuer Timeout, Retry, User-Agent.
5. ✅ 9 neue Tests (112 gesamt), alle gruen auf net8.0 und net10.0.

### Deliverables
1. One-liner Nutzung fuer Standardfaelle.
2. Klar getrennte Advanced-Konfiguration fuer Power User.

### DoD
1. Sample-App zeigt One-liner und Proxy-Use-Cases.
2. API Surface stabil und eindeutig benannt.

## Phase 5 - Robustheit und Qualitaetsnetz ✅

### Ziel
Technische Robustheit fuer Produktion und LLM-Tooling absichern.

### Erledigte Tasks
1. ✅ Golden-File Suite (News, Docs/API, Blog, Wikipedia, GitHub README) mit 5 HTML-Fixtures.
2. ✅ 40+ Golden-File Tests: Titel, Sektionen, Code-Erhaltung, Sidebar/Nav-Filterung, Cross-Archetype-Vertraege.
3. ✅ Failure-Injection Tests: 429 retry, 500 recovery, 503 all-retries, 403 no-retry, HttpRequestException retry, Cancellation.
4. ✅ Broken/Malformed HTML: unclosed tags, empty body, whitespace-only, script-heavy, deep nesting, huge content, plain text, special chars.
5. ✅ Chunking-Determinismus: 10 Tests (stabile IDs, identische Ausgabe, URL-abhaengige IDs, Hex-Format, ContiguousIndices).
6. ✅ JSON-Schema-Kompatibilitaet v1: required fields, null-omission, round-trip, forward-compat, camelCase, type-checks.
7. ✅ Edge-Case/Security: XSS/Script-Filterung, Event-Handler-Filterung, javascript:-URL-Filterung, Style-Stripping, RTL, Zero-Width-Chars, Relative-URL-Resolution.
8. ✅ 92 neue Tests (204 gesamt), alle gruen auf net8.0 und net10.0.

### Deliverables
1. Stabile Regression-Suite.
2. Dokumentierte Fehlermodellierung.

### DoD
1. Regression-Suite gruen.
2. Keine Breaking-Change-Verletzung im v1-Schema.

## Phase 6 - Benchmarks und Performance Gates ✅

### Ziel
Leistung messbar machen und regressionssicher halten.

### Erledigte Tasks
1. ✅ Benchmark-Projekt `Ndggr.Benchmarks` mit BenchmarkDotNet 0.14.0.
2. ✅ `SearchParsingBenchmarks`: Parse basic, instant answer, pagination results.
3. ✅ `ContentExtractionBenchmarks`: Readability, Markdown-Conversion, Link-Extraction, Chunking, Full Pipeline.
4. ✅ InternalsVisibleTo fuer Benchmarks in Ndggr und Ndggr.Content.
5. ✅ Solution um benchmarks-Folder erweitert.
6. ✅ Dry-Run verifiziert (net8.0): ~188µs basic parse, ~51µs instant answer parse.

### Deliverables
1. Reproduzierbare Benchmark-Pipeline.
2. Performance-Gate in CI und Release Workflow.

### DoD
1. Keine unerkannte Performance-Regression in PRs.
2. Benchmarks als Artefakte verfuegbar.

## Phase 7 - Packaging, Release und README ✅

### Ziel
Produktionsreife Auslieferung fuer CLI und NuGet.

### Erledigte Tasks
1. ✅ NuGet Pack fuer `Ndggr`, `Ndggr.Content`, `Ndggr.Extensions` (Version 0.1.0, MIT, Tags, Repository).
2. ✅ Self-contained Single-File CLI Binaries konfiguriert fuer:
   - win-x64, win-arm64
   - linux-x64, linux-arm64
   - osx-x64, osx-arm64
3. ✅ Release Workflow (`release.yml`) mit Tag-Trigger (v*), Build+Test, NuGet Pack, CLI Publish, GitHub Release.
4. ✅ README mit:
   - Vorteile (Single-file exe, library, fetch, proxy, json/jsonl)
   - Quickstarts fuer CLI und Library (Search + Fetch)
   - DI Integration Beispiel
   - ContentDocument JSON Schema v1 Dokumentation
   - Projektstruktur, Environment Variables, Requirements

### Deliverables
1. Erstes lauffaehiges Release (v0.x).
2. Vollstaendige Doku fuer Installation und Nutzung.

### DoD
1. GitHub Release inklusive Binaries erfolgreich.
2. NuGet Publish erfolgreich.
3. README deckt CLI, Library, Proxy und Fetch vollstaendig ab.

## Querschnittsarbeit in allen Phasen

1. Strict Coding Standards (`nullable`, warnings as errors, deterministic builds).
2. Kleine PRs mit klarer Scope-Grenze.
3. Jede neue Funktion bekommt Tests in derselben Phase.
4. Keine stillen API-Breaks ohne Change-Log.

## Empfohlene Reihenfolge der ersten 4 Umsetzungs-Sprints

1. Sprint 1: Phase 0 + Phase 1 Kernpfad.
2. Sprint 2: Phase 2 Proxy/Parity.
3. Sprint 3: Phase 3 Fetch MVP (Readability + ReverseMarkdown).
4. Sprint 4: Phase 4 DX + Phase 5 erste Robustheitswelle.

## Exit-Kriterien fuer v1.0

1. Search und Fetch in CLI und Library stabil.
2. Proxy funktioniert End-to-End in beiden Pfaden.
3. JSON-Schema v1 stabil und dokumentiert.
4. Golden-File Pass Rate >= 95%.
5. Release-Pipeline erzeugt NuGet-Pakete und Single-File Binaries fuer alle Zielplattformen.
