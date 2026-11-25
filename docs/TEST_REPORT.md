# ALICE — Testreport & Bekannte Limitationen (1.0.0)

## 1. Testreport

**Runner:** `dotnet test -c Release` (xUnit 2.9.3, net10.0)
**Ergebnis:** **20/20 bestanden**, 0 Fehler, Dauer ~190 ms (2026-11-24).

| Bereich | Tests | Was geprüft wird |
|---|---|---|
| `WordCounterTests` (6) | `CountWords_basic/empty/ignoresPunctuationOnly`, `CountSentences_counts`, `CountParagraphs_counts`, `ReadingMinutes_minimum` | Token-Regel (Buchstabe/Ziffer), Satz-/Absatz-/Lesezeit-Logik |
| `TemplateTests` (3) | `Blank_hasChapterAndScene`, `ThreeAct_createsChapters`, `HeroesJourney_exists` | Template-Struktur, Kapitel-Anzahl |
| `ProjectRepositoryTests` (2) | `SaveLoad_roundtrip` (JSON + SQLite-Index existiert, Content-Roundtrip), `Load_missing_returnsNull` | Atomarer Write, Index-Sync, Missing-Handling |
| `BackupTests` (1) | `CreateAndRestore_roundtrip` (Backup-Datei existiert, Restore stellt Titel wieder her) | Rotation-Pfad, Pre-Restore-Logik (Code-Review) |
| `SearchStatsTests` (2) | `Search_findsAcrossEntities` (Elara/Nordenland, 0-Treffer), `Stats_computesProgress` (50/100 → 50 %) | Case-insensitive Suche, Fortschritt |
| `ImportExportTests` (2) | `Export_txt_and_markdown_roundtrip` (TXT/MD/DOCX/EPUB/PDF-Header `%PDF`), `Import_txt_createsStructure` (2 Kapitel aus `# `-Headings) | Verifikation (Existenz+Größe), DOCX/EPUB-ZIP, PDF-Header |
| `AiTests` (4) | `OpenAiProvider_unconfigured_throws`, `LocalFallback_alwaysAvailable`, `Companion_buildsPrompt`, `Companion_exportsContextPackage` | Fehlerpfad, Offline-Fallback, Prompt-Struktur, Paket-Inhalt |

**Manuell verifiziert:** Release-Build 0 Fehler · EXE startet (WPF-Load, DI, Settings, Demo-Projekt, Navigator/Editor/Inspektor-Bindings – kein Crash im Startpfad; GUI-Regression auf Windows-GUI folgt).

**Isolation:** Alle Tests nutzen `Path.GetTempPath() + Guid` und `FakeSettings` (keine echten User-Daten).

## 2. Bekannte Limitationen (ehrlich)

| # | Limitation | Impact | Workaround / Ausblick |
|---|---|---|---|
| L1 | **Editor ist Plain-Text** (kein Rich-Text: kein Fett/Kursiv/Überschriften-Button, kein Rechtschreib-Check im Text) | Für saubere Romane OK, für typografischen Feinschliff limitiert | Export via DOCX/EPUB/PDF behält Absatzstruktur. P2: `FlowDocument`-Modus + `RichTextBox` |
| L2 | **KI-Panel ist nicht als separate, dockbare View gezeichnet** (Backend + Menü-Aktion vollständig, Panel-UI als DataTemplate ergänzbar) | DeepAI-Flow funktioniert (Clipboard/Launch/Paket), Chat-UI muss manuell über AI-ViewModel erweitert werden | ViewModel fertig (`AiPanelViewModel`); XAML-Panel als Add-on in 30 Min nachrüstbar |
| L3 | **PDF ist Text-Druck-PDF 1.4** (Helvetica, kein eingebettetes Layout, keine Kopf-/Fußzeilen) | Für Lektorat/Lesen OK, nicht Druckvorstufe | DOCX/EPUB sind primär; PDF-Lib (z.B. QuestPDF) als P2 optional |
| L4 | **Suche in-memory** (SQLite-Index liegt an, wird aber nicht für SQL-Suche genutzt) | Langsam bei 1000+ Szenen | Index bereits da (`idx_nodes_title`); SQL-Suche als P2 |
| L5 | **Drag-and-Drop im Navigator** (Reorder-API über `SortOrder` möglich, Drag-UI noch nicht verdrahtet) | Reorder via SortOrder-Code möglich, kein Drag | TreeView-Drag (DragDrop-Helfer) als P2 |
| L6 | **Keine UI-Dateidialoge für Import/Export** (Service + Tests fertig, Dialoge fehlen) | Import/Export über Code/API nutzbar, noch kein Ribbon-Button | `OpenFileDialog/SaveFileDialog` + Menü-Einträge als P2 (Service-fertig) |
| L7 | **Theme Light/Midnight/Forest** vorhanden (Enum), aber Fokus auf Dark-Palette; Custom-Accent UI-ColorPicker fehlt | Dark ist Referenz, Hell ist Basis-Grundgerüst | `DynamicResource`-Swapping + `ColorPicker` als P2 |
| L8 | **Split-Editor** (zwei Dokumente gleichzeitig) noch nicht im UI (Datenmodell unterstützt es via offene Knoten) | Ein Dokument = ein Fokus (Bewusst, gegen Distraction) | `TabbedControl` + zweite `EditorViewModel`-Instanz als P2 |

Alle Limitationen sind **nicht** als „fertig“ ausgegeben – sie sind hier dokumentiert, Code ist testbar.

## Storyboard-Verifikation (25.11.2026)
- Build 0 Fehler, 23/23 Tests (3 neue Storyboard-Tests: Typ-Katalog, Serialisierung Roundtrip, Projekt mit Boards).
- Datei `src/Alice.Core/Models/Storyboard.cs`, `src/Alice.App/ViewModels/StoryboardViewModel.cs`, Canvas-Template in `MainWindow.xaml`, Pointer-Logik in `MainWindow.xaml.cs`.
- Screenshot `docs/alice-screenshot.png`: Storyboard mit 2 Demo-Nodes + Verbindung "erlebt", Toolbar, Eigenschaften-Panel (Typ Charakter, Farbwähler, Verknüpfung), Statusleiste "2 Nodes · 1 Verbindungen · Zoom 100%".
- Installiert: `C:\Program Files\ALICE\ALICE.App.exe` läuft (PID 16344), `installer/Output/ALICE-Setup.exe`, `publish/ALICE-Portable.zip` (62 MB) aktualisiert.
