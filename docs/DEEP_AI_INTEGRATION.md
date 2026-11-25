# ALICE — DeepAI-Code-Integration (Report)

**Datum:** 2026-11-24 · **Umgebung:** DeepAI Code (Electron) unter `%LocalAppData%/Programs/deepai-code-desktop/DeepAI Code.exe`
**Methode:** Legitime Inspektion (Installationspfad, `server-runtime.json`, lokale HTTP-Probes mit `Accept: application/json`, `server.asar`-Stringanalyse). **Kein Reverse-Engineering privater Protokolle, kein Code-Injection, kein Credential-Extract.**

---

## 1. Befund

- **Lokaler Server:** `http://127.0.0.1:3773` (aus `server-runtime.json`: `host 127.0.0.1`, `port 3773`, `pid 8784`). TCP-Listener aktiv (`netstat`).
- **HTTP-Verhalten:** `GET /` → SPA-HTML. `GET /api/projects` mit `Accept: application/json` → `{"_tag":"EnvironmentAuthInvalidError","code":"auth_invalid","reason":"missing_credential"}`. Andere Pfade fallen auf SPA-HTML zurück.
- **`server.asar`-Routen (Stringanalyse, kein JS-Extract im Detail):** `/api/auth/session`, `/api/auth/websocket-ticket`, `/api/auth/pairing-token`, `/api/auth/pairing-links`, `/api/orchestration/shell`, `/api/orchestration/threads/`, `/api/projects`, `/api/projects/mutate`, `/api/model`, `/api/provider`, `/api/session`, `/api/fs/read`, `/api/fs/find`, u.a. Alle sind **session-gebunden** (`pairing-token`, `clients`, `mint-credential`, `relay-config`).
- **Credential-Speicher:** `%LocalAppData%/…/userdata/secrets/` enthält `server-signing-key.bin`, `mcp-oauth-signing-key.bin` u.a. (binär, nicht gelesen, nicht extrahiert – bewusst).
- **Fazit:** Es gibt **keinen dokumentierten öffentlichen API-Key** für externe Apps. Direkter Zugriff ohne Session = Auth-Bypass → **nicht zulässig und nicht implementiert**.

## 2. Umgesetzte Integration: Begleitworkflow (legitim)

`IDeepAiCompanionService` / `DeepAiCompanionService` (`src/Alice.Data/AiServices.cs`):

| Fähigkeit | Implementierung | Status |
|---|---|---|
| **EXE finden** | `TryFindDeepAiExe`: Settings-Pfad → `%LocalAppData%/Programs/deepai-code-desktop/DeepAI Code.exe` → Fallback-Pfad | ✅ Implementiert |
| **Server prüfen** | `IsServerReachable`: `GET DeepAiServerUrl` (2-s-Timeout) → Detail-String für Statusleiste/Custom-Settings | ✅ Implementiert |
| **Starten** | `LaunchDeepAi`: `Process.Start(UseShellExecute=true)`, `FileNotFoundException` wenn nicht gefunden | ✅ Implementiert |
| **Prompt kopieren** | `CopyPromptToClipboardAsync` (Temp `.md` sichern) + UI-`Clipboard.SetText` | ✅ Implementiert |
| **Kontextpaket** | `ExportContextPackageAsync`: Markdown (Titel/Autor/Genese/Synopsis/Manuskript/Figuren/Welt/Zeitlinie) → Zielpfad | ✅ Implementiert |
| **Strukturierter Prompt** | `BuildStructuredPrompt(task, context)`: System-Rolle + Aufgabe + Kontext + Regeln (kein Auto-Overwrite, Deutsch, Konsistenz) | ✅ Implementiert |
| **UI-Aktion** | Menü **KI → Mit DeepAI Code analysieren** → Kontext (Auswahl/Szene + Figuren) sammeln → Clipboard → Launch → Paket im Temp → Statusdetail (`AnalyzeDeepAiCommand`) | ✅ Implementiert |
| **Direkte lokale API** | Nicht implementiert (erfordert Session-Credential) | **Unzuverlässig / nicht unterstützt** |

## 3. Nicht implementierte Mechanismen (bewusst)

- Endlos-Suchen nach undokumentiertem Protokoll im `server.asar`
- Auslesen/Decodieren von `secrets/*.bin`
- Token-Minting via `/api/connect/mint-credential` ohne Nutzeraktion
- IPC/Named-Pipe-Hack, Executable-Modifikation, Injection

**Begründung:** Verstoß gegen Integration Rules (Auth umgehen, Credentials extrahieren, proprietäres Protokoll reverse-engineeren).

## 4. Empfohlene Nutzung

1. In ALICE Szene/Kapitel im Navigator wählen.
2. Menü **KI → Mit DeepAI Code analysieren** → Prompt landet in Zwischenablage, DeepAI Code öffnet sich, Kontextpaket liegt in `%TEMP%/ALICE-Kontext-*.md`.
3. In DeepAI Code einfügen/attach → Antwort → in ALICE **Ins Manuskript einfügen** (mit Vorschau/Bestätigung, kein stiller Overwrite).
4. Statusleiste zeigt Server-Detail (erreichbar / nicht erreichbar + Hinweis auf Session-Modell).

## 5. Zukunftsoption (nur bei offizieller Doku)

Falls DeepAI Code künftig einen öffentlichen Local-API-Key / OAuth-Pairing-Flow dokumentiert, kann `DeepAiDirectProvider : IAiProvider` als Adapter ergänzt werden (Basis-URL = `DeepAiServerUrl`, Key aus sicherem Store). Die Begleitworkflow-Schicht bleibt als Fallback bestehen.
