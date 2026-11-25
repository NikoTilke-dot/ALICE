# ALICE — KI-Provider-Dokumentation

## 1. Architektur

Modularer Adapter über `IAiProvider` (DI: `OpenAiCompatibleProvider` + `LocalFallbackProvider`).

```csharp
interface IAiProvider {
  string Id { get; } string DisplayName { get; }
  Task<bool> IsAvailableAsync(CancellationToken ct);
  Task<string> CompleteAsync(string system, string user, string context, CancellationToken ct);
  IAsyncEnumerable<string> StreamAsync(...); // SSE mit Fallback
}
```

**Prinzipien:** Kein Auto-Overwrite (Vorschau → Bestätigung → Einfügen), Kontext-Vorschau (`ContextPreview`) zeigt exakt, was gesendet wird, Cancellation überall, App bleibt ohne KI voll nutzbar (LocalFallback + DeepAI-Begleitung).

## 2. OpenAI-kompatibler Provider

- **Basis-URL:** z.B. `https://api.openai.com/v1` (Settings `AiBaseUrl`), Endpoint `POST {BaseUrl}/chat/completions`.
- **Model:** Settings `AiModel` (z.B. `gpt-4o-mini`).
- **Key:** `ALICE_AI_API_KEY`-ENV **oder** `AiApiKeyRef` aus Settings (nichts ins Log, nur `Authorization: Bearer`-Header). `ohne-key` = ohne Header (z.B. lokaler Server).
- **Timeout:** 3 Minuten. Fehler: HTTP-Status + Body-Ausschnitt (400 Zeichen) als `HttpRequestException`.
- **Streaming:** `stream=true`, SSE-Read (`data:`-Zeilen, `[DONE]`), Fallback auf non-stream `CompleteAsync` bei Fehler.
- **IsAvailable:** BaseUrl + Modell gesetzt, sonst `InvalidOperationException` mit Hinweis.

## 3. LocalFallbackProvider (`local-fallback`)

Offline, kein Netzwerk, immer verfügbar: beschreibt Aufgabe + Kontextlänge, weist auf Konfiguration / DeepAI-Begleitung hin. Für Tests und Demo ohne Key.

## 4. Kontext-Steuerung (KI-Panel)

Checkboxen pro Quelle: **Auswahl / Szene / Figuren / Welt** (persistiert in Settings). `ContextPreview` zeigt z.B. `[Auswahl: 123 Zeichen] + [Figuren: 5]`. `BuildContext` hängt **nur** Angehaktes an. **Nichts wird automatisch gesendet** – Senden nur per Button.

## 5. Schreib-Tools (Buttons im Panel)

- **Fortführen** (`ContinueWriting`), **Überarbeiten** (`Rewrite`: Klarheit/Fluss/Dialog), **Korrekturlesen** (`Proofread`: Liste mit Punkten), **Zusammenfassen** (`Summarize`).
Alle nutzen `Quick(instruction)` → `Send()` → Kontext.

## 6. Konversationen

Mehrere Conversations (Titel, Provider/Modell, System-Anweisung, Messages) – persistiert im Projekt (`AiConversations`). Einfügen ins Manuskript nur nach `MessageBox`-Bestätigung (Suffix `…`, Ziel: erster nicht-Ordner-Knoten).

## 7. DeepAI Code (separat)

Siehe `docs/DEEP_AI_INTEGRATION.md` – Begleitworkflow (Start + Clipboard + Kontextpaket), kein direkter API-Pfad ohne Session-Credential.
