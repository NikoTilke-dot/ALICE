using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Alice.Core.Models;
using Alice.Core.Services;
using Alice.Core.Settings;

namespace Alice.Data;

/// <summary>OpenAI-kompatibler Provider (Basis-URL + Modell + Key aus Settings, Key nie loggen).</summary>
public sealed class OpenAiCompatibleProvider : IAiProvider
{
    private readonly ISettingsStore _settings;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };
    public string Id => "openai-compatible";
    public string DisplayName => "OpenAI-kompatibel";

    public OpenAiCompatibleProvider(ISettingsStore settings) => _settings = settings;

    private string ApiKey => Environment.GetEnvironmentVariable("ALICE_AI_API_KEY") ?? _settings.Current.AiApiKeyRef ?? string.Empty;

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(!string.IsNullOrWhiteSpace(_settings.Current.AiBaseUrl) && !string.IsNullOrWhiteSpace(_settings.Current.AiModel));

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, string context, CancellationToken ct = default)
    {
        if (!await IsAvailableAsync(ct)) throw new InvalidOperationException("AI-Provider ist nicht konfiguriert. Bitte KI-Einstellungen prüfen.");
        var url = _settings.Current.AiBaseUrl.TrimEnd('/') + "/chat/completions";
        var body = new
        {
            model = _settings.Current.AiModel,
            stream = false,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = (string.IsNullOrWhiteSpace(context) ? "" : "KONTEXT:\n" + context + "\n\nAUFGABE:\n") + userPrompt }
            }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        var key = ApiKey;
        if (!string.IsNullOrWhiteSpace(key) && key != "ohne-key") req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var resp = await Http.SendAsync(req, ct);
        var txt = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new HttpRequestException($"AI-Provider antwortete {(int)resp.StatusCode}: {Trunc(txt, 400)}");
        using var doc = JsonDocument.Parse(txt);
        var msg = doc.RootElement.TryGetProperty("choices", out var ch) && ch.GetArrayLength() > 0
            ? ch[0].GetProperty("message").GetProperty("content").GetString() : string.Empty;
        return msg ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamAsync(string systemPrompt, string userPrompt, string context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Streaming über SSE, mit Fallback auf CompleteAsync — yield außerhalb try/catch
        var fallback = string.Empty;
        var failed = false;
        try
        {
            var url = _settings.Current.AiBaseUrl.TrimEnd('/') + "/chat/completions";
            var body = new
            {
                model = _settings.Current.AiModel,
                stream = true,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = (string.IsNullOrWhiteSpace(context) ? "" : "KONTEXT:\n" + context + "\n\nAUFGABE:\n") + userPrompt }
                }
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
            var key = ApiKey;
            if (!string.IsNullOrWhiteSpace(key) && key != "ohne-key") req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break;
                if (!line.StartsWith("data:")) continue;
                var data = line[5..].Trim();
                if (data == "[DONE]") break;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta").TryGetProperty("content", out var c) ? c.GetString() : null;
                    if (!string.IsNullOrEmpty(delta)) fallback += delta;
                }
                catch { }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { failed = true; }
        if (!string.IsNullOrEmpty(fallback)) { yield return fallback; yield break; }
        if (failed) yield return await CompleteAsync(systemPrompt, userPrompt, context, ct);
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}

/// <summary>Lokaler Offline-Provider (kein Netzwerk, einfache Heuristiken für Tests/Demo ohne Key).</summary>
public sealed class LocalFallbackProvider : IAiProvider
{
    public string Id => "local-fallback";
    public string DisplayName => "Lokal (offline)";
    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, string context, CancellationToken ct = default)
    {
        var outline = $"Lokal-Fallback (kein Netzwerk):\n\nAUFGABE: {userPrompt}\n\nKontextlänge: {context?.Length ?? 0} Zeichen. Bitte in den Einstellungen einen OpenAI-kompatiblen Provider konfigurieren oder den DeepAI-Code-Begleitworkflow nutzen.";
        return Task.FromResult(outline);
    }
    public async IAsyncEnumerable<string> StreamAsync(string systemPrompt, string userPrompt, string context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var t = await CompleteAsync(systemPrompt, userPrompt, context, ct);
        yield return t;
    }
}

/// <summary>DeepAI-Code-Begleitworkflow: legitim, ohne Auth-Bypass. Erkennt EXE, testet lokalen Server, kopiert Prompt, exportiert Kontextpaket.</summary>
public sealed class DeepAiCompanionService : IDeepAiCompanionService
{
    private readonly ISettingsStore _settings;
    public DeepAiCompanionService(ISettingsStore settings) => _settings = settings;

    public bool TryFindDeepAiExe(out string path)
    {
        var candidates = new[]
        {
            _settings.Current.DeepAiExePath,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "deepai-code-desktop", "DeepAI Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "deepai-code-desktop", "DeepAI-Code.exe"),
            @"C:\Users\nikot\AppData\Local\Programs\deepai-code-desktop\DeepAI Code.exe"
        };
        foreach (var c in candidates) if (!string.IsNullOrWhiteSpace(c) && File.Exists(c)) { path = c; return true; }
        path = string.Empty; return false;
    }

    public bool IsServerReachable(out string detail)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var url = (_settings.Current.DeepAiServerUrl ?? "http://127.0.0.1:3773").TrimEnd('/');
            var resp = http.GetAsync(url).GetAwaiter().GetResult();
            detail = $"Server antwortet ({(int)resp.StatusCode}) unter {url}. Direkter API-Zugriff erfordert eine DeepAI-Code-Sitzung (keinöffentlicher API-Key) — daher: Begleitworkflow mit Clipboard/Paket.";
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) { detail = $"Server nicht erreichbar: {ex.Message}"; return false; }
    }

    public void LaunchDeepAi()
    {
        if (TryFindDeepAiExe(out var exe))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        else throw new FileNotFoundException("DeepAI Code.exe nicht gefunden. Bitte Pfad in den Einstellungen angeben.");
    }

    public Task CopyPromptToClipboardAsync(string prompt)
    {
        // Läuft auf STA-Thread des Aufrufers; hier nur Text zurückgeben-zu-Kopieren via Zwischenablage in UI-Thread
        // Der Aufrufer (ViewModel) kopiert via Clipboard API. Hier: Datei zusätzlich sichern für Notfall.
        var tmp = Path.Combine(Path.GetTempPath(), $"ALICE-DeepAI-Prompt-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(tmp, prompt);
        return Task.CompletedTask;
    }

    public async Task<string> ExportContextPackageAsync(AliceProject project, string targetPath, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# ALICE Kontextpaket — {project.Title} ({DateTime.Now:yyyy-MM-dd HH:mm})");
        sb.AppendLine($"Autor: {project.Author} · Genre: {project.Genre} · Ziel: {project.TargetWordCount} Wörter");
        sb.AppendLine("\n## Synopsis\n" + project.Synopsis);
        sb.AppendLine("\n## Manuskript");
        foreach (var n in project.Nodes.OrderBy(n => n.SortOrder))
        {
            sb.AppendLine($"\n### {n.Title} [{n.Kind}] ({n.WordCount} Wörter)");
            if (!string.IsNullOrWhiteSpace(n.Synopsis)) sb.AppendLine($"Synopsis: {n.Synopsis}");
            sb.AppendLine(n.Content);
        }
        sb.AppendLine("\n## Figuren");
        foreach (var c in project.Characters) sb.AppendLine($"- {c.Name}: {c.Description} — {c.Personality}");
        sb.AppendLine("\n## Welt");
        foreach (var w in project.WorldEntries) sb.AppendLine($"- {w.Title} ({w.Category}): {w.Description}");
        sb.AppendLine("\n## Zeitlinie");
        foreach (var t in project.TimelineEvents.OrderBy(t => t.Date)) sb.AppendLine($"- {t.DateLabel}: {t.Title} — {t.Description}");
        var dir = Path.GetDirectoryName(targetPath)!; Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(targetPath, sb.ToString(), ct);
        return targetPath;
    }

    public string BuildStructuredPrompt(string task, string context)
        => $"Du bist Alice Intelligence, Schreibbegleiterin. \n\n## Aufgabe\n{task}\n\n## Kontext (vom Nutzer ausgewählt)\n{context}\n\n## Regeln\n- Schreibe auf Deutsch, wenn Aufgabe deutschsprachig ist.\n- Überschreibe nichts automatisch. liefere Vorschläge mit klarer Trennung.\n- Weist auf Inkonsistenzen zu Figuren/Welt/Zeitlinie hin.";
}
