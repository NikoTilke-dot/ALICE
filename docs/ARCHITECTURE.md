# ALICE — Architektur

**Version:** 1.0.0 · **Stack:** C# / .NET 10 · WPF · MVVM · SQLite · CommunityToolkit.Mvvm · Microsoft.Extensions.Hosting
**Zielplattform:** Windows 10/11 x64 · Local-First · Kein Cloud-Zwang

---

## 1. Projektstruktur (Solution: `Alice.slnx`)

```
alice/
├── Alice.slnx
├── src/
│   ├── Alice.Core/        # Domain-Modelle, Enums, Settings, Interfaces, Templates (net10.0)
│   ├── Alice.Data/        # Repositories, SQLite-Index, Backup, Suche, Statistik, KI-Provider, Import/Export (net10.0)
│   └── Alice.App/         # WPF-App: MainWindow, ViewModels, DataTemplates, DI-Setup (net10.0-windows)
├── tests/Alice.Tests/     # xUnit: 20 Tests (Core/Data/Provider/Import-Export)
├── assets/                # Original-Icon (SVG + ICO + PNG)
├── docs/                  # Architektur, DB, KI, Integration, Anleitung, Status
├── installer/             # Inno-Setup-Skript (wählbarer Installationspfad)
└── templates/             # (Reserved für nutzerdefinierbare Projekt-Vorlagen)
```

## 2. Module & Verantwortlichkeiten

| Modul | Aufgabe |
|---|---|
| `Alice.Core` | `Models`: `AliceProject`, `ManuscriptNode` (+`Snapshot`), `Character` (+Relationships), `WorldEntry`, `TimelineEvent`, `ResearchItem`, `AiConversation`, `WritingSession`, `StoryArc`, `WordCounter`. `Enums`: `NodeKind`, `StoryStatus`, `ExportFormat`, `ImportFormat`, `ThemeKind`, `WorkspaceMode`. `Settings`: `AliceSettings` (Theme, Editor-Font, Autosave, Backups, KI-Provider, DeepAI-Pfad, Ziele). `Services`: `IProjectRepository`, `ISettingsStore`, `IBackupService`, `IAiProvider`, `IDeepAiCompanionService`, `IImportExportService`, `ISearchService`, `IStatsService`. `Templates`: Story-Templates (3-Akt, 5-Akt, Heldenreise, Save-the-Cat, Mystery, Romance, Fantasy, Thriller). |
| `Alice.Data` | `ProjectRepository` (JSON `.alice.json` atomar + SQLite-Index `index.db` für Suche/Performance), `SettingsStore` (`%LocalAppData%/ALICE/settings.json`), `BackupService` (Rotation, Pre-Restore-Backup), `SearchService`, `StatsService`, `OpenAiCompatibleProvider` (Chat Completions + SSE-Stream mit Fallback), `LocalFallbackProvider` (offline, nie Blockieren), `DeepAiCompanionService` (EXE-Erkennung, Server-Health, Launch, Clipboard, Kontextpaket, strukturierter Prompt), `ImportExportService` (TXT/MD/DOCX/EPUB/PDF, Kapitel-Split an `# `-Headings, Verifikation: Datei muss existieren + nicht leer sein). |
| `Alice.App` | `App.xaml(.cs)`: DI (`ISettingsStore`, `IProjectRepository`, `IBackupService`, `ISearchService`, `IStatsService`, `IImportExportService`, `IDeepAiCompanionService`, 2×`IAiProvider`, alle ViewModels). `MainWindow`: Menü (Projekt/Bearbeiten/Ansicht/Schreiben/KI/Hilfe), Navigator (TreeView), Workspace-Tabs (Editor/Outliner/Corkboard/Figuren/Welt/Zeitlinie/Recherche/Dashboard/Statistik/Einstellungen), Inspektor, Statusleiste. `ViewModels`: `MainViewModel` (Lifecycle, Autosave-Timer, Navigator-Baum, DeepAI-Analyse), `EditorViewModel` (Text, Wortzähler, Undo/Redo, Find/Replace, Snapshots), `InspectorViewModel`, `Corkboard/Outliner/Characters/World/Timeline/Research/Dashboard/Stats/Settings/Search`, `AiPanelViewModel` (Kontext-Vorschau, Senden, Abbrechen, Einfügen nur mit Bestätigung). |

## 3. Datenfluss

1. **Öffnen:** `MainViewModel.OpenProjectPathAsync` → `IProjectRepository.LoadAsync` → JSON-Deserialize → SQLite-Index-Sync (best effort, blockiert Laden nie) → `RefreshAll` (Navigator-Baum, Editor, Inspektor, alle Workspaces).
2. **Editieren:** `EditorViewModel.Text` (PropertyChanged) → `ManuscriptNode.Content` → `WordCounter` aktualisiert live (Wörter/Zeichen/Sätze/Absätze/Lesezeit). Autosave-Timer (default 30 s) → `SaveAsync` (tmp + replace).
3. **Manuell:** Menü Speichern → `SaveAsync` → `SaveState` aktualisiert → Recent-Liste persistiert.
4. **Backup:** Nach Bedarf/manull → `BackupService.CreateBackupAsync` → `Backups/ALICE-<Titel>-<Timestamp>.alice.json` → Rotation (default 10). Restore liest Backup, sichert Ist-Stand als `pre-restore-*.alice.json` vor dem Überschreiben.
5. **KI:** `AiPanelViewModel.BuildContext` hängt nur angehakte Quellen an (Auswahl/Szene/Figuren/Welt) + `ContextPreview` zeigt klar, was gesendet wird → `IAiProvider.CompleteAsync` (HTTP, Timeout 3 Min, Cancellation). Auf Wunsch `StreamAsync` (SSE, Fallback auf Non-Stream). Antwort wird in `AiConversation` gespeichert, **Ins-Manuskript nur per Bestätigung** (`InsertToManuscript`).
6. **DeepAI Code:** Menü KI → `AnalyzeDeepAiCommand` → Kontext sammeln → `BuildStructuredPrompt` → Clipboard + `LaunchDeepAi` + Kontextpaket (Temp `.md`) + `IsServerReachable`-Detail in Statusleiste. **Kein Auth-Bypass, keine Credential-Extraktion.**
7. **Import/Export:** `ImportExportService` (siehe Modul). Export verifiziert Existenz + Größe, meldet Fehler statt Schweig-Erfolg.

## 4. Asynchron & Performance

- Alle I/O- und KI-Aufrufe `async`, UI-Thread wird nie blockiert; `CancellationToken` überall; KI-Generierung abbrechbar (`CancelCommand`).
- SQLite-Index + JSON-File-Trennung: Laden schnell, Suche über Index; Ah — `SearchService` läuft aktuell in-memory (einfach, deterministisch); der SQLite-Index (`nodes/characters/world` + `idx_nodes_title`) ist angelegt und wird bei jedem Save synchronisiert (erweiterbar auf SQL-Suche).
- Virtualisierung: `TreeView`/`ListBox` mit vielen Knoten — für „tausende Szenen" zusätzlich paginierte Listen / virtualisierende `ListView` empfohlen (siehe Known Limitations).
- Kein Blocker: `IsServerReachable` nutzt 2-s-Timeout.

## 5. Design System

Dark-Theme (Deep Charcoal `#0F1016`, Panels `#171822`, Akzent-Violett `#7C7CF4`, Muted `#9A9DB5`), Segoe UI / Georgia-Editor, sparsame Akzente, keine Über-Animation (Toggle in Settings). Original-Icon (Mond + Stern, kein Alice-in-Wonderland-Artwork).

## 6. Was bewusst nicht getan wurde

- Keine Scrivener-Kopie (eigene Struktur, eigene Iconographie).
- Kein DeepAI-Eigenprotocol (siehe `docs/DEEP_AI_INTEGRATION.md`).
- Keine Hardcoded API-Keys (Settings + `ALICE_AI_API_KEY` ENV, nur `Authorization: Bearer`, nie im Log).
