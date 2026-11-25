# ALICE — Projektstatus (1.0.0)

**Stand:** 2026-11-24 · **Build:** Release, 0 Fehler · **Tests:** 20/20 bestanden

---

## Status-Legende
`✅ Implementiert` = Code + UI + persistiert · `🧪 Getestet` = Unit/Integration-Test erfolgreich · `⚠️ Teilweise` = Kern da, Feinheiten offen · `🚧 Geplant` / `❌ Unsupported`

| Feature | Status | Nachweis |
|---|---|---|
| **App-Shell, Menüs, Toolbar, Statusleiste** | ✅ + 🧪 (Build) | `MainWindow.xaml` (Projekt/Ansicht/Schreiben/KI/Hilfe), `MainViewModel` Commands |
| **Navigator (Baum, +Kapitel/+Szene, Löschen mit Bestätigung)** | ✅ | `NavigatorItems` + `TreeView`, `AddNode/DuplicateNode/DeleteNode` (rekursiv) |
| **Editor (Rich-Plaintext, Find/Replace, Undo/Redo, Snapshots, Wortzähler)** | ✅ + 🧪 | `EditorViewModel`: Text→Node, `WordCounter` (W/Z/S/A/Lesezeit), `DoFind/DoReplace`, `CreateSnapshot/RestoreSnapshot` (mit Sicherungs-Snapshot) |
| **Multi-Mode Workspace (10 Tabs)** | ✅ | `ModeIndex`→`CurrentWorkspace`, DataTemplates in `MainWindow.xaml` |
| **Corkboard / Outliner** | ✅ | `CorkboardViewModel` (WrapPanel-Karten, Color/Status/Wörter), `OutlinerViewModel` (DataGrid) |
| **Figuren / Welt / Zeitlinie / Recherche / Dashboard / Statistik** | ✅ | CRUD-ViewModels + `Refresh` aus `AliceProject`, Felder in `DATABASE.md` |
| **Projekt-Lifecycle (Neu/Öffnen/Speichern/Duplik./Archiv/Löschen, Recent-Liste)** | ✅ + 🧪 | `IProjectRepository` (JSON atomar + SQLite-Index), `InitializeAsync` (Demo-Projekt), Tests: SaveLoad, Load_missing |
| **Autosave + Backups (Rotation, Restore mit Pre-Backup)** | ✅ + 🧪 | Timer (default 30 s), `BackupService` (Retention 10), Tests: CreateAndRestore |
| **Suche (Manuskript/Figuren/Welt/Recherche)** | ✅ + 🧪 | `SearchService`, Tests: findsAcrossEntities, 0-Treffer |
| **Statistik (Wörter/Zeichen/Szenen/Kapitel/Fortschritt, Daily/Session)** | ✅ + 🧪 | `StatsService` (`Compute/Daily`), `WritingSession`, Tests: computesProgress |
| **Templates (Blank, 3-Akt, 5-Akt, Heldenreise, Save-the-Cat, Mystery, Romance, Fantasy, Thriller)** | ✅ + 🧪 | `StoryTemplates.All` + `CreateFromTemplate`, Tests: Blank/ThreeAct/HeroesJourney |
| **Import (TXT/MD/DOCX/RTF – Kapitel-Split)** | ✅ + 🧪 | `ImportExportService.ImportAsync`, Tests: Import_txt |
| **Export (TXT/MD/DOCX/EPUB/PDF – verifiziert)** | ✅ + 🧪 | `ExportAsync` (Existenz+Größe-Check), Tests: Export_txt_and_markdown (TXT/MD/DOCX/EPUB/PDF-Header) |
| **KI-Provider (OpenAI-kompatibel, SSE+Fallback, Cancellation, LocalFallback)** | ✅ + 🧪 | `OpenAiCompatibleProvider` (3-Min-Timeout, Bearer, kein Log), `LocalFallbackProvider`, Tests: Unconfigured_throws, LocalFallback, BuildsPrompt |
| **KI-Panel (Kontext-Vorschau, Konversationen, Tools, Einfügen nur mit Bestätigung)** | ✅ | `AiPanelViewModel` (Checkboxen persistiert, `ContextPreview`, `Send/Cancel/InsertToManuscript`, Rewrite/Continue/Proofread/Summarize) |
| **DeepAI Code (Begleitworkflow: EXE-Detect, Health, Launch, Clipboard, Kontextpaket, strukturierter Prompt)** | ✅ + 🧪 | `DeepAiCompanionService`, `AnalyzeDeepAiCommand`, Tests: BuildsPrompt, ExportsContextPackage. **Direkte API: nicht unterstützt (Auth)** – siehe `DEEP_AI_INTEGRATION.md` |
| **Settings (Theme, Fonts, Autosave, Backup, KI, DeepAI-Pfad, Ziele)** | ✅ | `AliceSettings` + `SettingsStore`, `SettingsViewModel`, DataTemplate |
| **Icon / Version / Installer-Skript** | ✅ | `assets/alice.ico` (SVG→PNG→ICO, Mond+Stern, original), `AssemblyVersion 1.0.0`, `installer/ALICE-Setup.iss` |
| **Portable-Build** | ✅ | `dotnet publish -r win-x64 --self-contained` (siehe Release-Notes) |

## Bugs (bekannt, stand 2026-11-24)

- Keine blockierenden Bugs. Build 0 Fehler, 20 Tests grün.

## Technische Entscheidungen

- **JSON als Source of Truth** (portabel) + **SQLite als regenerierbarer Index** (statt EF Core) – weniger Native-Deps, Notebooks versionierbar.
- **Plain-Text-Editor** (WPF `TextBox`, Markdown-freundlich) statt voll rich-text-`FlowDocument` – bewusst: Robustheit + Export-Simple. Rich-Features (Bold/Italic-Buttons) als P2 vorgeschlagen (siehe Known Limitations).
- **Keine teuren PDF/EPUB-Libs** – DOCX/EPUB via OpenXML-ZIP, PDF als valides PDF 1.4 (Drucklayout). Ausbaubar.
- **DeepAI nur Begleitworkflow** – kein Auth-Bypass (siehe Integration-Report).

## Tests / Build

- `dotnet test -c Release`: **20/20 bestanden** (WordCounter, Templates, Repository, Backup, Search/Stats, Import/Export, AI/Provider/Companion)
- `dotnet build -c Release`: **0 Fehler, 0 Warnungen (außer MVVMT-Notices)**
