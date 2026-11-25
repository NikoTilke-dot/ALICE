# ALICE — Benutzerhandbuch

**Kurz:** *Your world. Your words. Your story.*

ALICE ist ein Local-First Schreibstudio (Windows 10/11 x64, .NET 10 Runtime). Alle Projekte liegen als `.alice.json` auf ihrem PC – kein Account, keine Cloud.

## 1. Erste Schritte

1. ALICE starten → erstellt automatisch unter `Dokumente/ALICE Projekte/Mein erster Roman.alice.json` ein Demo-Projekt (Drei-Akt-Vorlage).
2. **Navigator** (links): Kapitel/Szenen anlegen (`+ Kapitel`, `+ Szene`), auswählen, duplizieren, löschen (mit Bestätigung).
3. **Editor** (Mitte): Titel + Synopsis oben, Text darunter. Wortzähler live (Wörter/Zeichen/Sätze/Absätze/Lesezeit). Find/Replace unten, Snapshots (Name/Zeit, Wiederherstellen nur mit Bestätigung + Auto-Snapshot des aktuellen Stands).
4. **Speichern:** Autosave alle 30 s (einstellbar) + manuell (Menü Projekt → Speichern, Strg-nah). Statusleiste zeigt „Gespeichert / Autosave HH:mm:ss“.
5. **Modi-Tabs:** Editor, Outliner (Tabelle), Corkboard (Karten), Figuren, Welt, Zeitlinie, Recherche, Dashboard (Fortschritt), Statistik, Einstellungen.

## 2. Menüs

- **Projekt:** Neu / Öffnen / Speichern / Archivieren / Löschen (mit Auto-Backup) / Backup jetzt / Backup wiederherstellen.
- **Ansicht:** Navigator/Inspektor ein-/ausblenden, Fokusmodus (alles verstecken), Typewriter-Flag.
- **Schreiben:** Snapshot erstellen.
- **KI:** DeepAI-Analyse (siehe unten), DeepAI Code starten.
- **Hilfe:** Über ALICE.

## 3. Inspektor (rechts)

Metadaten zum selektierten Knoten: Kind, Status, Wörter/Ziel, Synopsis, POV (Figuren-Name), Ort, Tags, Notizen. Verschiebbare Trennlinien + ausblendbar.

## 4. Figuren & Welt & Zeitlinie & Recherche

- **Figuren:** Liste links, Edit rechts (Name/Rolle/Beschreibung/Persönlichkeit/Hintergrund/Ziele). Beziehungen, Arc, Voice, Custom Fields in `DATABASE.md` beschrieben; UI editiert Kernfelder, vollständige Felder bleiben im JSON erhalten.
- **Welt:** Einträge (Titel/Kategorie/Beschreibung), Hierarchie über `ParentId` (Land→Stadt), Verknüpfungen.
- **Zeitlinie:** Ereignisse mit freier `DateLabel` (fiktive Kalender OK), optionales Datum, Timeline-Name für parallele Linien.
- **Recherche:** Notizen/Links, Tags, verknüpft an Kapitel/Szenen.

## 5. KI-Panel (OpenAI-kompatibel + lokal)

- **Provider:** Settings → KI-Anbieter / Basis-URL / Modell / API-Key (oder ENV `ALICE_AI_API_KEY`). Lokal-Fallback immer verfügbar.
- **Kontext:** Checkboxen Auswahl/Szene/Figuren/Welt → Vorschau zeigt exakt, was gesendet wird. **Nichts wird automatisch gesendet.**
- **Senden:** Eingabefeld → Generieren (Abbrechen möglich) → Antwort als `KI:` im Verlauf. Buttons: Fortführen / Überarbeiten / Korrekturlesen / Zusammenfassen. **Einfügen nur mit Bestätigung** (an ersten nicht-Ordner-Knoten angehängt).

## 6. DeepAI Code (Begleitworkflow)

Menü **KI → Mit DeepAI Code analysieren**:
1. Kontext (Szene + Figuren) → strukturierter Prompt.
2. Prompt in Zwischenablage + Kontextpaket in `%TEMP%` + DeepAI Code öffnet sich.
3. In DeepAI Code einfügen/attach → Antwort zurück in ALICE einfügen (Vorschau).

Kein direkter API-Pfad ohne Session (siehe `DEEP_AI_INTEGRATION.md`).

## 7. Import / Export

- **Import:** `.md`/`.txt`/`.docx`/`.rtf` → Kapitel-Split an `# `-Headings (andere Headings als Text). Über Code/API (`IImportExportService`) oder künftige UI-Dialoge.
- **Export:** `TXT`, `Markdown`, `DOCX` (OpenXML), `EPUB` (ZIP+OPF/NCX/XHTML), `PDF` (valides PDF 1.4, Drucklayout). Verifikation: Datei muss existieren + Größe > 0, sonst Fehler statt Schweig-Erfolg.

## 8. Backup & Recovery

- **Manuell:** Projekt → Backup jetzt erstellen → `Backups/ALICE-<Titel>-<DatumUhrzeit>.alice.json`.
- **Rotation:** Default 10 Stapel (Settings `BackupRetention`).
- **Restore:** Projekt → Backup wiederherstellen → Dateiwahl → Ist-Stand wird als `pre-restore-*.alice.json` gesichert, dann wiederhergestellt + neu geladen.

## 9. Einstellungen

Theme (Dark/Light/Midnight/Forest – Dark default), Akzent, Fonts (UI/Editor), UI-Skala, Panel-Breiten, Autosave-Intervall, Backup-Retention, Ziele (Session/Daily 1000), KI + DeepAI-Pfad/URL. Persistiert in `%LocalAppData%/ALICE/settings.json`.

## 10. Barrierefreiheit

Tastatur-Navigation (Tab/Enter), sichtbare Fokus-Indikatoren (WPF-Standard), ein-/ausblendbare Panels, Skalierbare UI, Monospace-freie Editor-Font (Georgia), High-Contrast-taugliche Dark-Palette, reduzierte Animationen (Toggle).

## 11. Häufige Probleme

| Symptom | Ursache / Lösung |
|---|---|
| „Projekt nicht gefunden“ beim Öffnen | Pfad verschoben → Datei neu wählen; Recent-Liste zeigt Tooltip-Pfad. |
| KI: „nicht konfiguriert“ | Settings → Basis-URL + Modell setzen oder `local-fallback` wählen. |
| KI: HTTP 401 | Key ungültig → Settings / ENV prüfen. |
| DeepAI: „nicht gefunden“ | Settings → `DeepAiExePath` setzen oder DeepAI Code default installieren. |
| Export „fehlgeschlagen“ | Schreibrecht im Zielordner prüfen (kein schweigender Fehler). |

## Storyboard (Whiteboard)

Freie Canvas-Fläche für Mindmapping, Worldbuilding und Plotplanung. Button "Storyboard" in der Toolbar oder Menü Planung > Storyboard.

- Pan: Hand-Werkzeug oder Mittelklick + Ziehen. Zoom: Mausrad (um Cursor), Zoom +/−, Alles zeigen.
- Node erstellen: Rechtsklick auf freie Fläche (Notiz, Charakter, Ort, Welt, Ereignis, Idee) – Node entsteht am Klickpunkt. Oder "+ Notiz".
- Node bearbeiten: Titel/Text direkt anklicken und tippen. Verschieben: Kopf ziehen. Größe: Griff unten rechts ziehen. Einklappen: Häkchen im Eigenschaften-Panel (rechts, erscheint bei Auswahl).
- Node-Typen (14): Allgemeine Notiz, Charakter, Ort, Welt, Fraktion, Ereignis, Kapitel, Szene, Gegenstand, Geheimnis, Konflikt, Idee, Recherche, Eigene Kategorie – jeweils mit Farbe + Symbol.
- Verbindung: Node-Kontextmenü (Rechtsklick) > "Neues verbundenes Schreibfeld" (erstellt Node + Linie), oder "Verbindung"-Button (Quelle+Ziel anklicken), oder Port-Kreis am Node ziehen. Linie folgt Node-Bewegungen, Beschriftung + Typ (8 Arten) im Eigenschaften-Panel editierbar.
- Mehrfachauswahl: Shift+Klick, gemeinsam verschieben. Gruppe: "Gruppe aus Auswahl" (Rahmen um Auswahl).
- Suche: oben rechts Text eingeben + Suchen > Ergebnis anklicken springt zum Node. Filter: Typ-Textfeld. Raster ein/aus. Undo/Redo: Buttons oder Strg+Z/Y (außerhalb von Textfeldern).
- Mehrere Boards: Dropdown oben links, "+ Board", "Board löschen".
- ALICE-Verknüpfung: Eigenschaften-Panel > Verknüpfung: Art (Figur/Welt/Manuskript/Zeitlinie) + Zielobjekt wählen; Vorschau zeigt den Namen. Ändert das verknüpfte Objekt nicht.
- KI: Kontext-Umfang wählen (Ausgewählte Nodes / Gesamtes Whiteboard / Ausgewählte Gruppe) > "KI-Kontext kopieren" (in DeepAI Code einfügen) oder "Nodes aus Zwischenablage" (KI-Antwort als Zeilen > nach Bestätigung neue Idea-Nodes). Die KI verändert niemals automatisch Nodes.
- Speicherung: Boards sind Teil des Projekts (.alice.json), inkl. Pan/Zoom, Autosave wie bisher. Nach erneutem Öffnen: gleiche Anordnung.
- Grenzen (transparent): Kein PNG/PDF-Export und kein Druck (Toolbar-Optional), keine automatische Routenfindung der Linien (gerade Linien), kein Vollbildmodus.
