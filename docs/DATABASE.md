# ALICE — Datenbank & Projektformat

## 1. Projektformat: `.alice.json` (FormatVersion `1.0`)

Jedes Projekt ist **eine portable JSON-Datei** – ohne Cloud, ohne Account, kopierbar & versionierbar.

```json
{
  "Id": "guid",
  "Title": "Mein Roman",
  "Author": "…",
  "Synopsis": "…",
  "Genre": "Fantasy",
  "TargetWordCount": 80000,
  "TemplateId": "three-act",
  "CreatedAt": "2026-…",
  "UpdatedAt": "2026-…",
  "FormatVersion": "1.0",
  "Nodes": [ /* ManuscriptNode[] */ ],
  "Characters": [ /* Character[] */ ],
  "WorldEntries": [ /* WorldEntry[] */ ],
  "TimelineEvents": [ /* TimelineEvent[] */ ],
  "Research": [ /* ResearchItem[] */ ],
  "AiConversations": [ /* AiConversation[] */ ],
  "Sessions": [ /* WritingSession[] */ ],
  "StoryArcs": [ /* StoryArc[] */ ]
}
```

**Pfade:** `Dokumente/ALICE Projekte/<Titel>.alice.json` (einstellbare Root in Settings) + `Backups/` im selben Ordner.

## 2. Manuskript-Hierarchie (`ManuscriptNode`)

`ParentId`-Baum (flache Liste, UI baut Baum):

```
Series → Book → Part → Chapter → Scene → SubScene
Serien/Bücher/Teile: Kind (NodeKind)
                         Ordner (IsFolder=true), SortOrder (Drag-Reorder bereit)
                         Content (plain text, Markdown-freundlich)
                         Snapshots[] (Name, Beschreibung, Content, Zeit)
```

Metadaten pro Knoten: `Title`, `Synopsis`, `Status` (Idea…Completed), `PovCharacterId`, `Location`, `Color`, `TargetWordCount`, `Tags`, `Notes`, `LinkedCharacterIds/WorldIds/ResearchIds`, `IsFavorite`, `CreatedAt/UpdatedAt`.

**Abgeleitete Zähler** (`WordCounter`): `WordCount` (Tokens mit Buchstabe/Ziffer), `CharCount`, `SentenceCount`, `ParagraphCount`, `ReadingMinutes` (200 W/Min).

## 3. Referenz-Systeme

- **Figuren** (`Character`): Profil (Name, Alias, Alter, Rolle, Aussehen, Beschreibung, Persönlichkeit, Beruf, Hintergrund), Psychologie (Ziele, Motivation, Ängste, Stärken/Schwächen, Geheimnisse, Konflikte, Arc, Voice), `Relationships[]` (Typ: Familie/Freund/Feind/Romantik/Rivale/Verbündeter/Mentor/Custom + Beschreibung), `Appearances[]` (Knoten-Ids), `CustomFieldsJson`.
- **Welt** (`WorldEntry`): `Category` (Country…Custom), `ParentId` für Hierarchie (Land→Stadt→Ort), `RelatedWorldIds/CharacterIds`, `CustomFieldsJson`.
- **Zeitlinie** (`TimelineEvent`): `DateLabel` frei (fiktive Kalender OK), optionales `Date`, `Kind` (Story/Character/World/Chapter/Scene), `TimelineName` (parallele Timelines), Links zu Knoten/Figuren.
- **Recherche** (`ResearchItem`): `Kind` (Note/Link/File/Quote), `Url`, `Tags`, `LinkedNodeIds`.
- **KI** (`AiConversation`): `ProviderId/ModelId/SystemInstruction`, `Messages[]` (user/assistant/system).
- **Statistik** (`WritingSession`): `Date`, `WordsAdded/SessionWords`, `Duration`, `WordsTotalAtEnd`.

## 4. SQLite-Index (`index.db` neben der JSON)

Beim Laden/Speichern wird best-effort synchronisiert (Fehler blockieren nie):

```sql
CREATE TABLE nodes(id TEXT PRIMARY KEY, title TEXT, content TEXT, kind TEXT);
CREATE TABLE characters(id TEXT PRIMARY KEY, name TEXT, content TEXT);
CREATE TABLE world(id TEXT PRIMARY KEY, title TEXT, content TEXT);
CREATE INDEX idx_nodes_title ON nodes(title);
```

Zweck: Grundlage für künftige SQL-Suche/Performance. **Source of Truth bleibt die JSON-Datei**, der Index ist regenerierbar (löschen ohne Datenverlust).

## 5. Einstellungen

`%LocalAppData%/ALICE/settings.json` (`AliceSettings`): Theme/Akzent/Fonts/UI-Skala, Navigator/Inspektor/AI-Visibility + Breiten, Editor (Font, Größe, Zeilenabstand, Max-Breite, SpellCheck-Flag, Typewriter, Focus), Autosave/Backup (Intervall, Retention, Pfad), Recent-Liste (10, Pin), KI (Provider, BaseUrl, Modell, Key-Ref, Kontext-Flags, DeepAI-Pfad/URL), Ziele (Session/Daily), Animationen, Density.

## 6. Integrität

- Atomare Writes (`.tmp` + `Copy/Replace`) für JSON und Settings.
- Backup-Rotation + Pre-Restore-Backup vor jedem Restore.
- Export-Verifikation (Existenz + Größe > 0).
