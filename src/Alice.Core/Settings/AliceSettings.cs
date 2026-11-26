using Alice.Core.Enums;

namespace Alice.Core.Settings;

/// <summary>Anwendungs-Einstellungen (persistiert als JSON).</summary>
public sealed class AliceSettings
{
    public ThemeKind Theme { get; set; } = ThemeKind.Dark;
    public string AccentColor { get; set; } = "#7C7CF4";
    public string UiFont { get; set; } = "Segoe UI";
    public double UiScale { get; set; } = 1.0;
    public string EditorFont { get; set; } = "Georgia";
    public double EditorFontSize { get; set; } = 14;
    public double EditorLineHeight { get; set; } = 1.4;
    public double EditorMaxWidth { get; set; } = 720;
    public bool ShowNavigator { get; set; } = true;
    public bool ShowInspector { get; set; } = true;
    // Seitenlayout des Manuskript-Editors (Word-Ansichten)
    public double PageZoom { get; set; } = 1.0;
    public double PageMarginMm { get; set; } = 20.0;
    public string PageSize { get; set; } = "A4"; // A4 | Letter
    public string PageOrientation { get; set; } = "Portrait"; // Portrait | Landscape
    public bool ShowPageBorder { get; set; } = true;
    public bool ShowAiPanel { get; set; } = true;
    public double NavigatorWidth { get; set; } = 260;
    public double InspectorWidth { get; set; } = 300;
    public bool SpellCheck { get; set; } = false;
    public bool TypewriterMode { get; set; } = false;
    public bool FocusMode { get; set; } = false;
    public bool Autosave { get; set; } = true;
    public int AutosaveIntervalSec { get; set; } = 30;
    public bool AutoBackup { get; set; } = true;
    public int BackupRetention { get; set; } = 10;
    public string BackupFolder { get; set; } = string.Empty;
    public string RecentProjectsPath { get; set; } = string.Empty;
    public List<RecentProject> RecentProjects { get; set; } = [];
    public string ProjectsRootHint { get; set; } = string.Empty;
    public bool AiEnabled { get; set; } = true;
    public string AiProvider { get; set; } = "openai-compatible";
    public string AiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string AiModel { get; set; } = "gpt-4o-mini";
    public string AiApiKeyRef { get; set; } = string.Empty; // DPAPI-verwurzelt, nie im Klartext loggen
    public bool AiIncludeSelection { get; set; } = true;
    public bool AiIncludeScene { get; set; } = false;
    public bool AiIncludeCharacters { get; set; } = false;
    public bool AiIncludeWorld { get; set; } = false;
    public string DeepAiExePath { get; set; } = string.Empty;
    public string DeepAiServerUrl { get; set; } = "http://127.0.0.1:3773";
    public string DeepAiModel { get; set; } = "deepai/muse-ember";
    public int SessionWordGoal { get; set; } = 1000;
    public int DailyWordGoal { get; set; } = 1000;
    public bool Animations { get; set; } = false;
    public string LayoutDensity { get; set; } = "Comfortable";
}

public sealed class RecentProject
{
    public string Path { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime LastOpened { get; set; } = DateTime.UtcNow;
    public bool Pinned { get; set; }
}
