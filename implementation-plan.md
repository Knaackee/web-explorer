# ndggr - Implementation Plan

Dieser Plan ist der operative Umsetzungsplan zu `plan.md` und priorisiert schnelle Lieferbarkeit, robuste Qualität und saubere Releases.

## Leitprinzipien

1. Library und CLI sollen gleich einfach nutzbar sein.
2. ReverseMarkdown bleibt in Phase 1 der einzige Markdown-Converter.
3. Proxy muss durchgaengig funktionieren: CLI und Library.
4. Readability ist Primary fuer Main-Content-Extraction.
5. Jede Phase endet mit klaren Abnahmekriterien.

## Phase 0 - Setup und Entscheidungen fixieren

### Ziel
Projektbasis, Namensraum und Build-Standards final aufsetzen.

### Tasks
1. Solution und Projektnamen finalisieren (`ndggr.sln`, `Ndggr.*`).
2. Repo-Struktur gemaess Zielbild aus `plan.md` anlegen.
3. `Directory.Build.props` und `.editorconfig` konfigurieren.
4. Baseline CI (`restore`, `build`, `test`) erstellen.
5. Lizenzdatei und Package-Metadaten (MIT) vorbereiten.

### Deliverables
1. Buildbare leere Solution mit allen Projekten.
2. Gruene CI fuer einen leeren Build.

### DoD
1. `dotnet build` erfolgreich auf net8.0 und net10.0.
2. CI Pipeline gruen bei Pull Request.

## Phase 1 - Core Search (Ndggr + Ndggr.Cli)

### Ziel
DuckDuckGo Suche (HTML) als robuste Library und einfache CLI bereitstellen.

### Tasks
1. `DdgClient` mit `SearchAsync(query, SearchOptions)` implementieren.
2. HTML Parsing mit AngleSharp fuer Trefferliste.
3. Modelle: `SearchResult`, `InstantAnswer`, `DdgSearchOptions`.
4. CLI `ndggr search` mit Basisoptionen (`--num`, `--region`, `--time`, `--site`).
5. Konsolenausgabe in Spectre.Console formatieren.

### Deliverables
1. Nutzbare Suche in Library und CLI.
2. Unit Tests fuer Parser und Optionsmapping.

### DoD
1. Suchanfragen liefern reproduzierbare Ergebnisstruktur.
2. Unit-Tests fuer Parser >= 90% der Kernpfade.
3. CLI Help und Basisbeispiele funktionieren lokal.

## Phase 2 - Proxy, Privacy und Feature Parity (Search)

### Ziel
Feature-Parity fuer Suchbereich inkl. Proxy und Privacy.

### Tasks
1. Proxy in CLI und Library aktivieren.
2. Proxy-Prioritaet umsetzen:
   1. Request-Option
   2. Client-Default
   3. Optional `HTTPS_PROXY`
3. Optionen: `--unsafe`, `--noua`, `--json`, `--expand`.
4. Instant Answer (`-i`) und Ducky (`-j`) implementieren.
5. Fehlerklassen fuer Netzwerk/Proxy/RateLimit definieren.

### Deliverables
1. Stabile Suche mit Proxy-End-to-End.
2. JSON-Ausgabe fuer Suchresultate.

### DoD
1. Proxy-Szenarien getestet: ohne Auth, mit Auth, 407, Timeout.
2. Regression bei Non-Proxy-Requests ausgeschlossen.
3. CLI und Library liefern gleiche fachliche Ergebnisse bei gleichen Optionen.

## Phase 3 - Content Fetch MVP (Ndggr.Content)

### Ziel
URL in strukturierten Inhalt umwandeln: Markdown und JSON.

### Tasks
1. `FetchAsync(url, FetchOptions)` implementieren.
2. Robustes HTTP-Fetching: Timeout, Retry, Redirect, Compression.
3. Main-Content Extraction mit Readability (Primary).
4. HTML -> Markdown mit ReverseMarkdown (nur diese Engine in Phase 1).
5. JSON-Ausgabe (`ContentDocument`) mit `schemaVersion=1`.
6. CLI `ndggr fetch <url>` mit `--format markdown|json|jsonl`.

### Deliverables
1. End-to-End Fetch Pipeline mit Markdown und JSON Output.
2. Erste Golden-File Testbasis.

### DoD
1. 20+ reale URL-Fixtures laufen stabil durch.
2. Golden-File Pass Rate >= 95% fuer v1-Fixture-Set.
3. Bei Fehlern strukturierte Error-Response mit Diagnosefeldern.

## Phase 4 - Library DX und API-Ergonomie

### Ziel
API soll fuer Nutzer so einfach sein wie CLI.

### Tasks
1. Simple Facade API:
   1. `SearchAsync("...")`
   2. `FetchMarkdownAsync("...")`
   3. `FetchJsonAsync("...")`
2. Advanced API mit `SearchOptions` und `FetchOptions`.
3. DI Integration (`services.AddNdggr(...)`).
4. Konsistente Defaults fuer Timeout, Retry, User-Agent.
5. API-Dokumentation und Minimalbeispiele.

### Deliverables
1. One-liner Nutzung fuer Standardfaelle.
2. Klar getrennte Advanced-Konfiguration fuer Power User.

### DoD
1. Sample-App zeigt One-liner und Proxy-Use-Cases.
2. API Surface stabil und eindeutig benannt.

## Phase 5 - Robustheit und Qualitaetsnetz

### Ziel
Technische Robustheit fuer Produktion und LLM-Tooling absichern.

### Tasks
1. Golden-File Suite ausbauen (News, Docs, Blogs, Wikipedia, GitHub).
2. Failure-Injection Tests: 429, 5xx, kaputtes HTML, Redirect-Loops.
3. Chunking deterministisch machen und testen.
4. JSON-Schema-Kompatibilitaet testen (additive-only in v1).
5. Security Check fuer Parser/Sanitizer-Dependencies.

### Deliverables
1. Stabile Regression-Suite.
2. Dokumentierte Fehlermodellierung.

### DoD
1. Regression-Suite gruen.
2. Keine Breaking-Change-Verletzung im v1-Schema.

## Phase 6 - Benchmarks und Performance Gates

### Ziel
Leistung messbar machen und regressionssicher halten.

### Tasks
1. Benchmark-Projekte fuer Search Parsing und Content Extraction bauen.
2. CI Performance Gate mit Ratio max 1.10 aktivieren.
3. Baseline-Messwerte pro Plattform dokumentieren.

### Deliverables
1. Reproduzierbare Benchmark-Pipeline.
2. Performance-Gate in CI und Release Workflow.

### DoD
1. Keine unerkannte Performance-Regression in PRs.
2. Benchmarks als Artefakte verfuegbar.

## Phase 7 - Packaging, Release und README

### Ziel
Produktionsreife Auslieferung fuer CLI und NuGet.

### Tasks
1. NuGet Pack fuer `Ndggr`, `Ndggr.Content`, `Ndggr.Extensions`.
2. Self-contained Single-File CLI Binaries fuer:
   1. win-x64, win-arm64
   2. linux-x64, linux-arm64
   3. osx-x64, osx-arm64
3. Release Workflow mit Tag-Trigger finalisieren.
4. README final schreiben:
   1. Weiterentwicklung von ddgr in .NET
   2. Vorteile (Single-file exe, library, fetch, proxy, json/jsonl)
   3. Quickstarts fuer CLI und Library

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
