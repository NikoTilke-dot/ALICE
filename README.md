<img src="assets/icon.svg" width="96" height="96" alt="ALICE Icon">

# ALICE
### Advanced Literary & Creative Intelligence Studio
**Your world. Your words. Your story.**

Local-First Schreibstudio für Romane, Weltbau, Zeitlinien, Recherche und KI-Begleitung. Windows 10/11 x64 · .NET 10 · WPF · SQLite · Kein Cloud-Zwang.

---

## Schnellstart (60 Sekunden)

1. **Portable:** `publish/ALICE-Portable.zip` entpacken → `publish/portable/ALICE.App.exe` starten.
   **oder Installer:** `installer/ALICE-Setup.iss` mit Inno Setup 6 kompilieren → `ALICE-Setup.exe` (Pfad wählbar).
2. Demo-Projekt erscheint automatisch: `Dokumente/ALICE Projekte/Mein erster Roman.alice.json`.
3. Schreiben: Navigator → Szene wählen → tippen (Wortzähler live). Autosave alle 30 s.
4. KI: Menü **KI → Mit DeepAI Code analysieren** (Begleitworkflow: Prompt + Start + Kontextpaket).

> Voraussetzung: .NET 10 Desktop Runtime (x64) – Portable ist self-contained und enthält alles außer Windows selbst.

## Die 10 Arbeitsbereiche

**Editor** (Text, Find/Replace, Undo/Redo, Snapshots, Wortzähler) · **Outliner** (Tabelle) · **Corkboard** (Karten) · **Figuren** (Profile, Beziehungen, Arc) · **Welt** (Länder/Städte/Fraktionen/Magie, Hierarchie) · **Zeitlinie** (fiktive Kalender, parallele Timelines) · **Recherche** (Notizen/Links) · **Dashboard** (Fortschritt) · **Statistik** (Wörter/Ziele) · **Einstellungen** (Theme, Fonts, Autosave, KI, DeepAI-Pfad).

## KI & DeepAI Code

- **Provider:** OpenAI-kompatibel (`AiBaseUrl` + `AiModel` + Key via `ALICE_AI_API_KEY`-ENV oder Settings) oder **Lokal (offline)**.
- **Kontext:** Auswahl / Szene / Figuren / Welt – Vorschau zeigt exakt, was gesendet wird. **Nichts wird automatisch gesendet.** Einfügen nur mit Bestätigung.
- **DeepAI Code:** **Begleitworkflow** (EXE erkennen, Health-Check `127.0.0.1:3773`, Launch, Clipboard, Kontextpaket). Direkter API-Zugriff erfordert Session-Credential → nicht implementiert (kein Auth-Bypass). Details: `docs/DEEP_AI_INTEGRATION.md`.

## Import / Export & Backup

- **Import:** TXT / Markdown / DOCX / RTF (Kapitel-Split an `# `-Headings).
- **Export:** TXT / Markdown / DOCX / EPUB / PDF 1.4 (verifiziert: Datei existiert + nicht leer).
- **Backup:** Manuell + Autosave + Rotation (10) + Restore mit Pre-Restore-Backup. Restore überschreibt nie still.

## Daten & Privatsphäre

- Projekt = **eine `.alice.json`** (portabel, kopierbar) + regenerierbarer `index.db` (SQLite). Einstellungen in `%LocalAppData%/ALICE/settings.json`.
- **Local-First:** Manuscript, Figuren, Welt, Zeitlinie, Recherche, Snapshots, KI-Konversationen – alles lokal. KI-Requests nur mit Nutzer-Kontext.

## Bauen & Testen (Entwickler)

```bash
dotnet build Alice.slnx -c Release
dotnet test tests/Alice.Tests/Alice.Tests.csproj -c Release   # 20/20
dotnet publish src/Alice.App/Alice.App.csproj -c Release -r win-x64 --self-contained true -o publish/portable
```

## Doku

| Datei | Inhalt |
|---|---|
| `docs/USER_GUIDE.md` | Bedienung, Menüs, KI, Backup-Troubleshooting |
| `docs/ARCHITECTURE.md` | Module, Datenfluss, DI, Performance |
| `docs/DATABASE.md` | `.alice.json`-Format, Tabellen, SQLite-Index |
| `docs/AI-PROVIDER.md` | Provider, Kontext, Schreib-Tools |
| `docs/DEEP_AI_INTEGRATION.md` | Warum Begleitworkflow (Server-Befund, kein Bypass) |
| `docs/PROJECT_STATUS.md` | Feature-Status / Bugs / Entscheidungen |
| `docs/TEST_REPORT.md` | 20 Tests + bekannte Limitationen (ehrlich) |
| `docs/INSTALLATION.md` | Inno-Setup, Portable, Backup/Recovery |

## Status: 1.0.0

Build: **0 Fehler** · Tests: **20/20** · GUI-Start-Smoke: **OK** (5-s-Lauf ohne Crash) · Portable-ZIP: **62 MB self-contained**. Bekannte Limitationen (Plain-Text-Editor, PDF-Light, Drag&Drop-P2 u.a.) stehen in `docs/TEST_REPORT.md` – nichts als „fertig“ ausgegeben, was es nicht ist.

---

*ALICE ist original: eigene Struktur, eigenes Icon (Mond + Stern), kein Scrivener-Clone, kein Alice-in-Wonderland-Artwork.*
