using System.Text.Json;
using Alice.Core.Models;
using Alice.Core.Services;
using Alice.Core.Settings;
using Microsoft.Data.Sqlite;

namespace Alice.Data;

/// <summary>JSON-Projektdatei (.alice.json) + SQLite-Index für Suche/Statistik.</summary>
public sealed class ProjectRepository : IProjectRepository
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public string ProjectDirFor(string projectFilePath) => Path.GetDirectoryName(Path.GetFullPath(projectFilePath))!;
    public string BackupDirFor(string projectFilePath) => Path.Combine(ProjectDirFor(projectFilePath), "Backups");

    public async Task<AliceProject?> LoadAsync(string projectFilePath, CancellationToken ct = default)
    {
        if (!File.Exists(projectFilePath)) return null;
        var json = await File.ReadAllTextAsync(projectFilePath, ct);
        var proj = JsonSerializer.Deserialize<AliceProject>(json, Json);
        // SQLite-Index best-effort synchronisieren (für Suche/Performance, Inhalt bleibt JSON als Source of Truth)
        try { EnsureIndex(projectFilePath, proj); } catch { /* Index darf Laden nie blockieren */ }
        return proj;
    }

    public async Task SaveAsync(AliceProject project, string projectFilePath, CancellationToken ct = default)
    {
        var dir = ProjectDirFor(projectFilePath);
        Directory.CreateDirectory(dir);
        project.UpdatedAt = DateTime.UtcNow;
        // Atomarer Write: tmp + replace
        var tmp = projectFilePath + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(project, Json), ct);
        File.Copy(tmp, projectFilePath, overwrite: true);
        File.Delete(tmp);
        try { EnsureIndex(projectFilePath, project); } catch { }
    }

    public async Task<List<AliceProject>> ListRecentAsync(IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        var list = new List<AliceProject>();
        foreach (var p in paths)
        {
            try { var pr = await LoadAsync(p, ct); if (pr is not null) list.Add(pr); } catch { }
            ct.ThrowIfCancellationRequested();
        }
        return list;
    }

    private void EnsureIndex(string projectFilePath, AliceProject? proj)
    {
        if (proj is null) return;
        var dbPath = Path.Combine(ProjectDirFor(projectFilePath), "index.db");
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS nodes(id TEXT PRIMARY KEY, title TEXT, content TEXT, kind TEXT);
                CREATE TABLE IF NOT EXISTS characters(id TEXT PRIMARY KEY, name TEXT, content TEXT);
                CREATE TABLE IF NOT EXISTS world(id TEXT PRIMARY KEY, title TEXT, content TEXT);
                CREATE INDEX IF NOT EXISTS idx_nodes_title ON nodes(title);
                """;
            cmd.ExecuteNonQuery();
        }
        using var tx = conn.BeginTransaction();
        using (var d = conn.CreateCommand()) { d.CommandText = "DELETE FROM nodes; DELETE FROM characters; DELETE FROM world;"; d.ExecuteNonQuery(); }
        foreach (var n in proj.Nodes)
        {
            using var c = conn.CreateCommand();
            c.CommandText = "INSERT INTO nodes(id,title,content,kind) VALUES($i,$t,$c,$k)";
            c.Parameters.AddWithValue("$i", n.Id.ToString()); c.Parameters.AddWithValue("$t", n.Title);
            c.Parameters.AddWithValue("$c", (n.Content ?? "")[..Math.Min(2000, (n.Content ?? "").Length)]);
            c.Parameters.AddWithValue("$k", n.Kind.ToString());
            c.ExecuteNonQuery();
        }
        foreach (var ch in proj.Characters)
        {
            using var c = conn.CreateCommand();
            c.CommandText = "INSERT INTO characters(id,name,content) VALUES($i,$n,$c)";
            c.Parameters.AddWithValue("$i", ch.Id.ToString()); c.Parameters.AddWithValue("$n", ch.Name);
            c.Parameters.AddWithValue("$c", (ch.Description + " " + ch.WhitespaceNotes()).Trim());
            c.ExecuteNonQuery();
        }
        foreach (var w in proj.WorldEntries)
        {
            using var c = conn.CreateCommand();
            c.CommandText = "INSERT INTO world(id,title,content) VALUES($i,$t,$c)";
            c.Parameters.AddWithValue("$i", w.Id.ToString()); c.Parameters.AddWithValue("$t", w.Title);
            c.Parameters.AddWithValue("$c", w.Description ?? "");
            c.ExecuteNonQuery();
        }
        tx.Commit();
    }
}

public static class CharacterExt
{
    public static string WhitespaceNotes(this Character c) => string.Join(" ", c.Personality, c.Background, c.Notes);
}

/// <summary>Settings-Store (JSON in LocalAppData/ALICE).</summary>
public sealed class SettingsStore : ISettingsStore
{
    public string SettingsPath { get; }
    public AliceSettings Current { get; private set; } = new();

    public SettingsStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ALICE");
        Directory.CreateDirectory(dir);
        SettingsPath = Path.Combine(dir, "settings.json");
    }

    public async Task<AliceSettings> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = await File.ReadAllTextAsync(SettingsPath, ct);
                Current = JsonSerializer.Deserialize<AliceSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AliceSettings();
            }
        }
        catch { Current = new AliceSettings(); }
        return Current;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
        var tmp = SettingsPath + ".tmp";
        await File.WriteAllTextAsync(tmp, json, ct);
        File.Copy(tmp, SettingsPath, overwrite: true);
        File.Delete(tmp);
    }
}

/// <summary>Backup-Service mit Rotation.</summary>
public sealed class BackupService : IBackupService
{
    private readonly IProjectRepository _repo;
    private readonly ISettingsStore _settings;
    public BackupService(IProjectRepository repo, ISettingsStore settings) { _repo = repo; _settings = settings; }

    public async Task<string> CreateBackupAsync(AliceProject project, string projectFilePath, CancellationToken ct = default)
    {
        var dir = _repo.BackupDirFor(projectFilePath);
        Directory.CreateDirectory(dir);
        var name = $"ALICE-{project.Title}-{DateTime.Now:yyyyMMdd-HHmmss}.alice.json";
        // Schutz vor ungültigen Zeichen
        foreach (var ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
        var dest = Path.Combine(dir, name);
        var json = JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(dest, json, ct);
        // Rotation
        var keep = Math.Max(1, _settings.Current.BackupRetention);
        var all = Directory.GetFiles(dir, "*.alice.json").OrderByDescending(File.GetLastWriteTime).ToList();
        foreach (var f in all.Skip(keep)) { try { File.Delete(f); } catch { } }
        return dest;
    }

    public Task<IReadOnlyList<string>> ListBackupsAsync(string projectFilePath, CancellationToken ct = default)
    {
        var dir = _repo.BackupDirFor(projectFilePath);
        if (!Directory.Exists(dir)) return Task.FromResult<IReadOnlyList<string>>([]);
        return Task.FromResult<IReadOnlyList<string>>(Directory.GetFiles(dir, "*.alice.json").OrderByDescending(File.GetLastWriteTime).ToList());
    }

    public async Task RestoreAsync(string backupFile, string targetProjectFile, CancellationToken ct = default)
    {
        if (!File.Exists(backupFile)) throw new FileNotFoundException("Backup nicht gefunden.", backupFile);
        // Vor Restore Backup des Ist-Stands
        var dir = _repo.BackupDirFor(targetProjectFile);
        Directory.CreateDirectory(dir);
        if (File.Exists(targetProjectFile)) File.Copy(targetProjectFile, Path.Combine(dir, $"pre-restore-{DateTime.Now:yyyyMMdd-HHmmss}.alice.json"));
        await File.WriteAllTextAsync(targetProjectFile, await File.ReadAllTextAsync(backupFile, ct), ct);
    }
}

/// <summary>Such-/Statistik-Services.</summary>
public sealed class SearchService : ISearchService
{
    public SearchResult Search(AliceProject project, string query)
    {
        var r = new SearchResult();
        if (string.IsNullOrWhiteSpace(query)) return r;
        var q = query.Trim().ToLowerInvariant();
        bool Hit(string? s) => s?.ToLowerInvariant().Contains(q) == true;
        r.ManuscriptHits = project.Nodes.Where(n => !n.IsFolder && (Hit(n.Title) || Hit(n.Content) || Hit(n.Synopsis) || Hit(n.Tags))).ToList();
        r.CharacterHits = project.Characters.Where(c => Hit(c.Name) || Hit(c.Description) || Hit(c.Background)).ToList();
        r.WorldHits = project.WorldEntries.Where(w => Hit(w.Title) || Hit(w.Description)).ToList();
        r.ResearchHits = project.Research.Where(x => Hit(x.Title) || Hit(x.Content)).ToList();
        return r;
    }
}

public sealed class StatsService : IStatsService
{
    public ProjectStats Compute(AliceProject project)
    {
        var chapters = project.Nodes.Where(n => n.Kind == Alice.Core.Enums.NodeKind.Chapter).OrderBy(n => n.SortOrder).ToList();
        var scenes = project.Nodes.Where(n => n.Kind == Alice.Core.Enums.NodeKind.Scene || n.Kind == Alice.Core.Enums.NodeKind.SubScene).ToList();
        var total = project.TotalWords;
        return new ProjectStats
        {
            TotalWords = total,
            TotalCharacters = project.Nodes.Sum(n => n.CharCount),
            TotalScenes = project.TotalScenes,
            TotalChapters = project.TotalChapters,
            ProgressPct = project.TargetWordCount <= 0 ? 0 : Math.Min(100, Math.Round(total * 100.0 / project.TargetWordCount, 1)),
            WordsPerChapter = chapters.Select(c => c.WordCount + project.ChildrenOf(c.Id).Sum(s => s.WordCount)).ToArray(),
            WordsPerScene = scenes.Select(s => s.WordCount).ToArray()
        };
    }

    public DayStats Daily(AliceProject project, DateOnly day)
    {
        var s = project.Sessions.Where(x => DateOnly.FromDateTime(x.Date) == day).ToList();
        return new DayStats { Day = day, Words = s.Sum(x => x.SessionWords), Duration = TimeSpan.FromTicks(s.Sum(x => x.Duration.Ticks)), Sessions = s.Count };
    }
}
