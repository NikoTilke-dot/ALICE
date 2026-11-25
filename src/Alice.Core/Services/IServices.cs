using Alice.Core.Enums;
using Alice.Core.Models;
using Alice.Core.Settings;

namespace Alice.Core.Services;

/// <summary>Projekt-Repository: Laden/Speichern einzelner .alice-Projekte (JSON + SQLite-Index).</summary>
public interface IProjectRepository
{
    Task<AliceProject?> LoadAsync(string projectFilePath, CancellationToken ct = default);
    Task SaveAsync(AliceProject project, string projectFilePath, CancellationToken ct = default);
    Task<List<AliceProject>> ListRecentAsync(IReadOnlyList<string> paths, CancellationToken ct = default);
    string ProjectDirFor(string projectFilePath);
    string BackupDirFor(string projectFilePath);
}

/// <summary>Einstellungs-Speicher.</summary>
public interface ISettingsStore
{
    AliceSettings Current { get; }
    Task<AliceSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    string SettingsPath { get; }
}

/// <summary>Backup/Recovery.</summary>
public interface IBackupService
{
    Task<string> CreateBackupAsync(AliceProject project, string projectFilePath, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListBackupsAsync(string projectFilePath, CancellationToken ct = default);
    Task RestoreAsync(string backupFile, string targetProjectFile, CancellationToken ct = default);
}

/// <summary>AI-Provider (modularer Adapter).</summary>
public interface IAiProvider
{
    string Id { get; }
    string DisplayName { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, string context, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(string systemPrompt, string userPrompt, string context, CancellationToken ct = default);
}

/// <summary>DeepAI-Code-Begleitworkflow (legitimer Weg: Start + Clipboard + Kontextpaket, kein Auth-Bypass).</summary>
public interface IDeepAiCompanionService
{
    bool TryFindDeepAiExe(out string path);
    bool IsServerReachable(out string detail);
    void LaunchDeepAi();
    Task CopyPromptToClipboardAsync(string prompt);
    Task<string> ExportContextPackageAsync(AliceProject project, string targetPath, CancellationToken ct = default);
    string BuildStructuredPrompt(string task, string context);
}

/// <summary>Import/Export.</summary>
public interface IImportExportService
{
    Task<AliceProject> ImportAsync(string filePath, ImportFormat format, CancellationToken ct = default);
    Task ExportAsync(AliceProject project, string outputPath, ExportFormat format, CancellationToken ct = default);
}

/// <summary>Projektsuche.</summary>
public interface ISearchService
{
    SearchResult Search(AliceProject project, string query);
}

public sealed class SearchResult
{
    public List<ManuscriptNode> ManuscriptHits { get; set; } = [];
    public List<Character> CharacterHits { get; set; } = [];
    public List<WorldEntry> WorldHits { get; set; } = [];
    public List<ResearchItem> ResearchHits { get; set; } = [];
    public int Total => ManuscriptHits.Count + CharacterHits.Count + WorldHits.Count + ResearchHits.Count;
}

/// <summary>Statistik-Berechnung.</summary>
public interface IStatsService
{
    ProjectStats Compute(AliceProject project);
    DayStats Daily(AliceProject project, DateOnly day);
}

public sealed class ProjectStats
{
    public int TotalWords { get; set; }
    public int TotalCharacters { get; set; }
    public int TotalScenes { get; set; }
    public int TotalChapters { get; set; }
    public double ProgressPct { get; set; }
    public int[] WordsPerChapter { get; set; } = [];
    public int[] WordsPerScene { get; set; } = [];
}

public sealed class DayStats
{
    public DateOnly Day { get; set; }
    public int Words { get; set; }
    public TimeSpan Duration { get; set; }
    public int Sessions { get; set; }
}
