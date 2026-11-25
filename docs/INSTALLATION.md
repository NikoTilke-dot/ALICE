# Installation & Backup/Recovery — ALICE 1.0.0

## Installation (Empfohlen: Inno-Setup, mit wählbarem Pfad)

1. `installer/ALICE-Setup.iss` mit Inno Setup 6 kompilieren (siehe Datei für Optionen) → `ALICE-Setup.exe`.
2. `ALICE-Setup.exe` ausführen → Standardpfad schlägt `C:\Programme\ALICE` vor, **frei wählbar** (Checkbox „Ordner wählen“). Kein stiller Zweit-Install.
3. Startmenü-Eintrag + Deinstallierer werden angelegt. Es werden keine weiteren Komponenten installiert.
4. Voraussetzung: **.NET 10 Desktop Runtime** (x64). ALICE inkl. SQLite nativ – ohne Internet installierbar.

## Portable (ohne Installation)

- `ALICE-Portable.zip` aus `publish/portable/` (self-contained win-x64) entpacken und `ALICE.App.exe` starten.
- Keine Registry-Einträge, keine PATH-Änderungen.

## Erster Start

- Legt an: `Dokumente/ALICE Projekte/Mein erster Roman.alice.json` (Drei-Akt-Demo) + `%LocalAppData%/ALICE/settings.json`.
- Kein Account, keine Cloud, kein Telemetrie-Zwang.

## Backup (manuell & automatisch)

- **Manuell:** Projekt-Menü → Backup jetzt erstellen → `Backups/ALICE-<Titel>-<Timestamp>.alice.json` neben dem Projekt.
- **Auto:** alle N Sekunden Autosave (JSON) + Backup-Rotation (default 10) bei Backup-Erstellung.
- **Restore:** Projekt → Backup wiederherstellen → Backup-Datei wählen → Ist-Stand wird als `pre-restore-*.alice.json` gesichert, dann Backup als aktives Projekt geladen. **Restore überschreibt nie still.**

## Integritäts-Check (empfohlen nach Restore)

1. Projekt öffnen → Dashboard zeigt Titel/Wörter/Szenen plausibel.
2. Editor: Knoten auswählen → Wörter/Zeichen zählen.
3. Suche (Strg+F-nahe): Treffer > 0 für bekannten Begriff.
4. Optional: `index.db` löschen → beim nächsten Speichern wird Index neu aufgebaut (kein Datenverlust).

## Deinstallation

- Inno: `Uninstall` im Startmenü / System → Einstellungen → Apps. Projekte in `Dokumente/ALICE Projekte` bleiben erhalten (bewusstes Design: Daten ≠ Programm).

## Weiterleitung

- **Nutzer:** `USER_GUIDE.md`
- **Datenformat:** `DATABASE.md`
- **KI/DeepAI:** `AI-PROVIDER.md` + `DEEP_AI_INTEGRATION.md`
