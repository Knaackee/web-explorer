# Playwright Session Plan

## Ziel

Playwright soll in der Library und in der CLI als optionaler Render-Fetcher mit Session-Konzept verfugbar sein.

Eine Session besitzt:

- eine stabile Session-ID
- einen eigenen BrowserContext oder persistenten Context
- isolierte Cookies, Local Storage und Session Storage
- Session-Metadaten wie Erstellungszeit, letzter Zugriff, Renderer-Optionen

Ein Aufruf mit Session-ID muss denselben Session-Zustand wiederverwenden.

## Nicht verhandelbare Invarianten

- CLI und Library sprechen dieselbe versionierte Session-Host-API.
- Eine Session besitzt genau einen Context.
- Ephemeral Sessions laufen in einem geteilten warmen Browser.
- Persistent Sessions laufen in einem dedizierten persistenten Browser-Context.
- Request-spezifische Optionen durfen Session-Invarianten nicht stillschweigend uberschreiben.
- Sessions haben einen klaren Lifecycle mit Ablauf oder explizitem Ende.
- Parallelitat ist erlaubt, aber nur als bewusst konfiguriertes Session-Verhalten.

## Harte Anforderung

CLI- und Library-Paritat ist nur erreichbar, wenn Sessions nicht nur in-memory im aufrufenden Prozess existieren.

Warum:

- Die Library kann einen warmen Browser im Host-Prozess halten.
- Die CLI ist heute ein One-shot-Prozess. Nach Prozessende sind in-memory Browser und Contexts weg.
- `wxp fetch --session <id>` in einem neuen CLI-Prozess kann dieselbe Session nur verwenden, wenn ein externer langlebiger Session-Host existiert.

Deshalb sollte das Design von Anfang an zwei Ebenen trennen:

1. Session-API: fachliche Operationen wie Start, End, Fetch.
2. Session-Host: langlebiger Prozess, der Browser und Contexts warm halt.

## Empfohlene Architektur

### 1. Neuer Playwright-Session-Host

Ein lokaler Hintergrundprozess verwaltet Browser, Contexts und Session-Metadaten.

Verantwortung:

- Playwright einmal initialisieren
- Chromium-Browser warm halten
- Sessions erzeugen und beenden
- pro Session einen Context verwalten
- Session-spezifische Cookies und Storage isolieren
- Requests aus CLI und Library entgegennehmen
- Browser bei Disconnect oder Crash kontrolliert neu starten

Der Host ist die einzige Instanz, die echte Playwright-Objekte besitzt.

### 2. Library als Client

Fur v1 sollte die Library denselben Out-of-process-Host nutzen wie die CLI.

Warum:

- nur ein kanonischer Session-Lifecycle
- nur eine Fehler- und Restart-Logik
- echte CLI- und Library-Paritat
- keine doppelte Implementierung fur in-process und out-of-process

Eine Embedded-Host-Option kann spater als Komfortschicht folgen, aber nicht als primare v1-Architektur.

### 3. CLI als Client

Die Library bekommt zwei Nutzungsmodi:

- In-process: .NET-Anwendung startet und nutzt den Session-Host intern
- Out-of-process: .NET-Anwendung verbindet sich zu einem bereits laufenden lokalen Host

Damit bleibt die API in der Library und CLI fachlich identisch.

Die CLI spricht nicht direkt mit Playwright, sondern mit dem Session-Host.

Vorteile:

- `start-session`, `fetch --session` und `end-session` funktionieren uber Prozessgrenzen hinweg
- Browserstart wird amortisiert
- Session-Zustand bleibt stabil
- spater sind auch Batch- oder Worker-Szenarien moglich

## Session-Modell

### Session-Typ

Standardmassig sollte eine Session genau einen BrowserContext besitzen.

Empfehlung:

- ein gemeinsamer Browser pro kompatibler Browser-Konfiguration
- ein BrowserContext pro Session
- pro Fetch eine neue Page innerhalb des Session-Contexts

Das ist der beste Kompromiss aus:

- warmem Browser
- isolierten Cookies
- sauberem Session-Lifecycle
- guter Parallelisierbarkeit

### Keine dedizierten Browser pro Session als Default

Nicht empfohlen als Standard:

- ein Browser pro Session

Gruende:

- hoher RAM-Verbrauch
- schlechtere Skalierung
- langsameres Startverhalten
- unnotig fur Cookie- und Storage-Isolation

Ein eigener Browser pro Session kann spater als Spezialmodus hinzukommen, etwa fur Anti-Bot-Falle oder streng getrennte Proxies.

### Session-Klassen

Es gibt zwei verschiedene Session-Klassen, die architektonisch getrennt behandelt werden sollten:

1. Ephemeral Session
    Context lebt in einem geteilten warmen Browser und verschwindet bei `end-session` oder Host-Ende.

2. Persistent Session
    Context verwendet `userDataDir` und lauft in einem dedizierten persistenten Browser-Launch.

Wichtig:

- Persistent Sessions sind keine Untervariante eines normalen `browser.newContext()`-Flows.
- Persistent Sessions durfen deshalb nicht im selben Pooling-Modell wie Ephemeral Sessions beschrieben werden.
- Proxy- oder Profil-Parameter einer Persistent Session gehoren zur Session-Identitat.

Empfehlung fur v1:

- Sessions standardmassig ephemeral
- optional `Persistent = true` in API und CLI

Damit bleibt v1 einfach, aber die Architektur blockiert kein spateres Resume-Feature.

### Session-Concurrency

Eine Session kann konzeptionell mehrere gleichzeitige Pages haben. Das ist technisch mit Playwright moglich und fur Dienste wie Jina sehr wahrscheinlich Teil des Durchsatzmodells.

Das Problem ist nicht Playwright selbst, sondern gemeinsamer Session-Zustand:

- mehrere Pages teilen Cookies und Storage
- parallele Navigationsfolgen konnen Login- oder Anti-Bot-Zustande beeinflussen
- JS-seitige Mutationen oder serverseitige Session-Updates konnen sich gegenseitig uberschreiben

Deshalb sollte der Plan nicht sagen "nur ein Fetch gleichzeitig, Punkt", sondern:

- sicherer Default: `MaxConcurrentFetchesPerSession = 1`
- opt-in fur parallele Nutzung: `MaxConcurrentFetchesPerSession > 1`
- klare Dokumentation: parallel innerhalb derselben Session ist shared-state parallelism und kann Seiteneffekte haben

Empfehlung fur v1:

- Session-default `1`
- Host-global dennoch viele Sessions parallel
- spater konfigurierbar pro Session

So bekommt ihr beides:

- sichere stateful Sessions fur Login-Flows
- hohen Durchsatz uber viele Sessions oder uber bewusst freigeschaltete Parallelitat innerhalb einer Session

## Vorgeschlagene CLI

### Neue Commands unter Root

Neue Commands direkt unter dem Root-Command:

- `wxp start-session`
- `wxp end-session <session-id>`
- `wxp list-sessions`
- `wxp inspect-session <session-id>`

Optional spater:

- `wxp start-host`
- `wxp stop-host`
- `wxp host-status`

### Fetch-Integration

`fetch` bekommt neue Optionen:

- `--renderer http|playwright|auto`
- `--session <session-id>`
- `--wait-until load|domcontentloaded|networkidle`
- `--timeout-ms <ms>`
- `--playwright-headless <true|false>`

Regeln:

- `--session` impliziert `--renderer playwright`
- `--renderer http` mit `--session` ist ein Fehler
- `fetch --session <id>` verwendet exakt den Context dieser Session

### Beispiel-CLI

```bash
wxp start-session
wxp start-session --session my-login
wxp fetch https://example.com/login --session my-login
wxp fetch https://example.com/account --session my-login --format json
wxp inspect-session my-login
wxp end-session my-login
```

## Vorgeschlagene Library-API

### Neue Kernabstraktionen

```csharp
public interface IPlaywrightSessionManager
{
    Task<PlaywrightSessionHandle> StartSessionAsync(
        PlaywrightSessionStartOptions? options = null,
        CancellationToken cancellationToken = default);

    Task EndSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlaywrightSessionInfo>> ListSessionsAsync(
        CancellationToken cancellationToken = default);

    Task<PlaywrightSessionInfo?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}
```

```csharp
public sealed record PlaywrightSessionStartOptions
{
    public string? SessionId { get; init; }
    public bool Persistent { get; init; }
    public string? UserDataDir { get; init; }
    public int MaxConcurrentFetches { get; init; } = 1;
    public Uri? Proxy { get; init; }
    public string? UserAgent { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}
```

```csharp
public sealed record PlaywrightSessionInfo
{
    public required string SessionId { get; init; }
    public required PlaywrightSessionState State { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset LastAccessedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool Persistent { get; init; }
    public int MaxConcurrentFetches { get; init; }
}
```

```csharp
public enum PlaywrightSessionState
{
    Starting,
    Active,
    Busy,
    Broken,
    Ending,
    Ended,
    Expired
}
```

### Fetch-Optionen erweitern

`ContentExtractionOptions` sollte nicht mit zu viel Session- und Render-Infrastruktur uberladen werden.

Besser ist eine Trennung zwischen:

- Extraction-Optionen
- Render-Optionen
- Session-Optionen

Vorschlag:

- `ContentExtractionOptions` bleibt fur Extraction, Chunking und Ausgabe zustandig
- neue `BrowserRenderOptions` fur Playwright-spezifische Request-Parameter
- `SessionId` lebt auf dem Fetch-Request, nicht als fachfremde Eigenschaft in jedem Extraction-Objekt

Vorschlag:

```csharp
public enum ContentRenderMode
{
    Http,
    Playwright,
    Auto
}
```

```csharp
public sealed record BrowserRenderOptions
{
    public ContentRenderMode RenderMode { get; init; } = ContentRenderMode.Http;
    public string WaitUntil { get; init; } = "networkidle";
    public int RenderTimeoutMs { get; init; } = 30_000;
}
```

Und fur den eigentlichen Fetch:

```csharp
public sealed record ContentFetchRequest
{
    public required string Url { get; init; }
    public string? SessionId { get; init; }
    public BrowserRenderOptions? Render { get; init; }
    public ContentExtractionOptions? Extraction { get; init; }
}
```

Regeln in der Library:

- `SessionId != null` erzwingt `RenderMode = Playwright`
- bei `RenderMode = Auto` darf spater ein HTTP-First-Ansatz mit Playwright-Fallback eingebaut werden
- in v1 ist `Auto` optional, `Http` und `Playwright` reichen

### Facade-Paritat

`WebExplorerClient` sollte Session-Methoden bekommen.

Vorschlag:

```csharp
public sealed class WebExplorerClient
{
    public Task<PlaywrightSessionHandle> StartPlaywrightSessionAsync(...);
    public Task EndPlaywrightSessionAsync(string sessionId, ...);
    public Task<IReadOnlyList<PlaywrightSessionInfo>> ListPlaywrightSessionsAsync(...);
}
```

Damit bleibt die Library-Nutzung einheitlich mit der CLI.

## Session-Host API

Der Host braucht eine kleine interne RPC- oder HTTP-Schnittstelle.

Minimal notwendige Operationen:

- `StartSession`
- `EndSession`
- `ListSessions`
- `GetSession`
- `FetchWithSession`
- `FetchWithEphemeralPlaywright`
- `Health`

Empfehlung fur v1:

- lokale HTTP-API auf Loopback oder Named Pipe

Empfehlung nach Betriebsaspekten:

- lokale RPC-Kommunikation als Primarmodus
- auf Windows bevorzugt Named Pipe
- auf Linux/macOS bevorzugt Unix Domain Socket
- Loopback-HTTP nur optional fur Debugging oder spatere Remote-Szenarien

Begrundung:

- keine Port-Kollisionen
- kleinere Angriffsflache
- einfachere lokale Berechtigungsgrenzen
- sauberere Semantik fur lokalen Session-Betrieb

## Host-Protokoll und Versionierung

Host und Clients brauchen eine explizite Kompatibilitatsschicht.

Minimal:

- `ProtocolVersion`
- `ServerVersion`
- `Capabilities`
- `HealthStatus`

Der erste Client-Handshake sollte diese Informationen prufen, bevor Requests abgesetzt werden.

## Session-Lifecycle

### Start

`start-session` macht:

1. Session-ID reservieren oder validieren
2. Browser fur passende Konfiguration holen oder starten
3. neuen Context fur die Session anlegen
4. Session registrieren
5. Session-Lease oder Ablaufzeit setzen
6. SessionInfo zuruckgeben

### Fetch mit Session

`fetch --session <id>` macht:

1. Session nachschlagen
2. im Session-Context neue Page erzeugen
3. URL laden
4. gerendertes HTML extrahieren
5. Page schliesen
6. bestehende Extraction-Pipeline weiterverwenden

Wichtig:

- Der Context bleibt offen.
- Nur die einzelne Page wird nach jedem Fetch geschlossen.
- Cookies und Storage bleiben dadurch an der Session hangen.
- Parallelitat innerhalb derselben Session unterliegt dem Session-Limit.

### End

`end-session` macht:

1. alle offenen Pages der Session schliessen
2. Context schliessen
3. Session aus Registry entfernen
4. falls kein Context mehr aktiv ist, Browser optional warm belassen oder nach Idle-Timeout schliessen

### Ablauf und verwaiste Sessions

Sessions durfen nicht unbegrenzt leben, nur weil ein CLI-Prozess nie `end-session` aufgerufen hat.

Deshalb sollte jede Session haben:

- `CreatedAt`
- `LastAccessedAt`
- `ExpiresAt` oder `IdleTimeout`

Empfehlung fur v1:

- Host-seitiger Idle-Timeout fur Sessions
- expliziter Lease-Refresh bei jeder Nutzung
- abgelaufene Sessions wechseln nach `Expired` und werden aufgeraumt

## Browser-Management

### Warm Browser

Der Host soll Browser pro kompatibler Konfiguration wiederverwenden.

Kompatibilitats-Hash mindestens aus:

- BrowserType
- Headless
- Proxy-Konfiguration auf Browser-Ebene
- Executable/Channel

Wenn Sessions inkompatible Startoptionen brauchen, werden mehrere Browser gehalten.

Session-Invarianten fur den Browser-Hash:

- BrowserType
- Headless
- Proxy-Konfiguration auf Browser-Ebene
- Executable oder Channel

Session-Invarianten durfen durch `fetch --session` nicht uberschrieben werden.

### Idle Shutdown

Empfehlung:

- Browser nach konfigurierbarem Idle-Timeout schliessen, wenn keine Sessions aktiv sind

Dadurch bleibt der Host ressourcenschonend, ohne Warmstart komplett zu verlieren.

### Crash / Disconnect

Der Host soll `Disconnected` behandeln:

1. Browser als tot markieren
2. alle betroffenen Sessions auf `Broken` setzen
3. neue Fetches entweder automatisch neu aufbauen oder klar fehlschlagen lassen

Empfehlung fur v1:

- Ephemeral Sessions nach Browser-Crash standardmassig auf `Broken` setzen
- Persistent Sessions nach Browser-Crash ebenfalls auf `Broken` setzen
- kein stiller Session-Rebuild mit neuer leerer State-Basis
- optional spater explizites `resume-session` oder `repair-session`

Begrundung:

- Eine Session steht fur Cookies und Login-Zustand.
- Ein stiller Neuaufbau ohne diesen Zustand ware semantisch falsch.

## Paritat zwischen CLI und Library

Paritat bedeutet:

- dieselben Session-Begriffe
- dieselben Fehlerfalle
- dieselben Metadaten
- dieselben Render-Optionen

Paritat bedeutet nicht zwingend:

- dass beide Oberflachen identische Klassen oder Flags haben

Empfehlung:

- CLI-Kommandos werden 1:1 auf SessionManager-Operationen gemappt
- Library und CLI nutzen denselben Host-Client intern

So gibt es genau eine Session-Implementierung und keine doppelte Logik.

## Provisioning und Browser-Binaries

Der Plan sollte explizit festhalten, wie Browser-Binaries bereitgestellt werden.

Mindestens zu entscheiden:

- on-demand beim ersten Playwright-Start
- expliziter Install-Schritt
- CI- und Release-Verhalten
- Fehlerbild bei fehlenden Binaries in CLI und Library

Empfehlung fur v1:

- klarer Install- oder Bootstrap-Schritt
- deterministische Fehlermeldung mit Wiederherstellungsweg
- kein verstecktes halbes Auto-Provisioning ohne Status-Ruckgabe

## Projektstruktur-Vorschlag

### Minimale Variante

- `src/WebExplorer.Playwright/`
  - `PlaywrightSessionManager.cs`
  - `PlaywrightHostClient.cs`
  - `PlaywrightHostServer.cs`
  - `PlaywrightBrowserPool.cs`
  - `PlaywrightSessionRegistry.cs`
  - `PlaywrightRenderClient.cs`

Integration:

- `WebExplorer.Content` verwendet `PlaywrightRenderClient` als optionalen Fetch-Pfad
- `WebExplorer.Extensions` registriert SessionManager und Host-Client
- `WebExplorer.Cli` bekommt neue Session- und Host-Commands auf Root-Ebene

### Alternative ohne neues Projekt

Technisch moglich, aber nicht empfohlen:

- alles direkt in `WebExplorer.Content`

Nachteile:

- hohere Paketlast fur Nutzer ohne Playwright
- unklarere Verantwortlichkeiten
- erschwerte optionale Installation

Empfehlung:

- Playwright in ein eigenes Projekt und optionales Paket auslagern

## Paket- und Betriebsmodell

Empfehlung:

- `WebExplorer.Content` bleibt leichtgewichtig
- `WebExplorer.Playwright` ist optionales Zusatzpaket
- `WebExplorer.Extensions` kann `AddWebExplorerPlaywright()` anbieten

CLI:

- `WebExplorer.Cli` referenziert das Playwright-Paket direkt
- bei erstem Playwright-Einsatz klare Fehlermeldung, falls Browser-Binaries fehlen

## Offene Entscheidungen

### 1. Host-Transport

Optionen:

- Named Pipe auf Windows
- Unix Domain Socket auf Linux/macOS
- Loopback-HTTP fur Debug oder Remote-Spater

Empfehlung: lokaler RPC-Transport statt HTTP als Primarmodus.

### 2. Session-ID-Erzeugung

Optionen:

- random ID als Default
- benannte Sessions erlauben

Empfehlung: beides. Wenn nicht angegeben, generierte ID. Wenn angegeben, Upsert verhindern und Konfliktfehler werfen.

### 3. Session-Resume nach Host-Restart

Optionen:

- v1 nein
- v1 nur fur persistent sessions

Empfehlung: v1 nur fur persistent sessions optional, ephemeral sessions nicht wiederherstellen.

### 4. Auto-Start des Hosts

Optionen:

- CLI startet Host implizit bei erstem Session-Befehl
- Nutzer startet Host explizit

Empfehlung: impliziter Start ist fur UX besser. Der Host sollte aber als eigener Prozess laufen und PID/Port/Token in einer lokalen State-Datei registrieren.

### 5. Session-Concurrency-Default

Optionen:

- immer genau 1
- standardmassig 1, konfigurierbar > 1
- unbegrenzt

Empfehlung: standardmassig 1, explizit konfigurierbar > 1.

## Implementierungsphasen

### Phase 1

- neues Playwright-Projekt anlegen
- Session-Host fur lokalen Prozesszugriff bauen
- Host-Handshake und Protokollversion bauen
- `start-session`, `end-session`, `list-sessions` in CLI
- `fetch --session` in CLI
- Session-API in `WebExplorerClient`
- Session-TTL und Cleanup bauen
- Ephemeral Sessions sauber abschliessen

### Phase 2

- DI-Registrierung verbessern
- Idle-Shutdown und Health-Checks
- Session-Inspektion erweitern
- Retry- und Disconnect-Strategien harten
- konfigurierbare Session-Concurrency
- Browser-Binary-Provisioning abrunden

### Phase 3

- persistente Sessions
- optionaler HTTP-First-then-Playwright-Fallback
- mehrere Browser nach Konfigurations-Hash
- Metriken und Benchmarks
- explizites Resume- oder Repair-Modell fur Persistent Sessions

## Teststrategie

Mindestens abdecken:

- Session-Start liefert stabile ID
- zwei Fetches mit derselben Session teilen Cookies
- zwei Sessions sind isoliert
- `end-session` entfernt Session und macht weitere Nutzung unmoglich
- CLI `start-session` -> `fetch --session` -> `end-session` funktioniert uber getrennte Prozesse
- Browser-Disconnect fuhrt zu sauberem Session-Status und nachvollziehbarem Fehler
- parallele Fetches mit verschiedenen Sessions blockieren sich nicht unnotig
- parallele Fetches in derselben Session respektieren `MaxConcurrentFetches`
- Session-TTL raumt verwaiste Sessions auf
- Session-Invarianten konnen durch Request-Optionen nicht verletzt werden

## Klare Empfehlung

Wenn `start-session` und `fetch --session` sowohl in CLI als auch Library stabil funktionieren sollen, dann braucht das Projekt einen langlebigen Session-Host.

Ein rein in-memory SessionManager reicht fur die Library, aber nicht fur CLI-Paritat.

Die beste v1 ist deshalb:

- Browser warm im Session-Host
- ein Context pro Session
- eine Page pro Fetch
- standardmassig ein gleichzeitiger Fetch pro Session, opt-in fur mehr
- CLI und Library als gemeinsame Clients auf dieselbe Session-API
- Playwright in eigenem optionalem Paket
