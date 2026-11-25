using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Alice.Core.Enums;
using Alice.Core.Models;
using Alice.Core.Services;
using Alice.Core.Settings;
using Alice.Core.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Alice.App.ViewModels;

/// <summary>Haupt-ViewModel: Projekt-Lifecycle, Navigator, Workspace-Switching.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IProjectRepository _repo;
    private readonly IImportExportService? _importExport;
    private readonly ISettingsStore _settings;
    private readonly IBackupService _backup;
    private readonly IDeepAiCompanionService _deepAi;
    private readonly NavigatorViewModel _nav;
    private readonly EditorViewModel _editor;
    private readonly InspectorViewModel _inspectorVm;
    private readonly CorkboardViewModel _cork;
    private readonly OutlinerViewModel _outliner;
    private readonly CharactersViewModel _chars;
    private readonly WorldViewModel _world;
    private readonly TimelineViewModel _timeline;
    private readonly ResearchViewModel _research;
    private readonly AiPanelViewModel _ai;
    private readonly DashboardViewModel _dash;
    private readonly StatsViewModel _stats;
    private readonly SettingsViewModel _settingsVm;
    private readonly SearchViewModel _search;
    private readonly StoryboardViewModel _board;

    public AliceProject? Project { get; set; }
    public string? ProjectPath { get; set; }
    private Timer? _autosaveTimer;
    private DateTime _lastSave = DateTime.UtcNow;

    [ObservableProperty] private string _statusText = "ALICE bereit.";
    [ObservableProperty] private string _saveState = "Ungespeichert";
    [ObservableProperty] private string _projectTitle = "Kein Projekt";
    [ObservableProperty] private int _totalWords;
    [ObservableProperty] private bool _showNavigator = true;
    [ObservableProperty] private bool _showInspector = true;
    [ObservableProperty] private bool _showAi = true;
    [ObservableProperty] private int _modeIndex; // 0 Editor, 1 Outliner, 2 Corkboard, ...
    [ObservableProperty] private bool _showEditorBar = true;
    [ObservableProperty] private object? _currentWorkspace;
    [ObservableProperty] private GridLength _navigatorWidth = new(260);
    [ObservableProperty] private GridLength _inspectorWidth = new(300);
    [ObservableProperty] private ObservableCollection<NavigatorNode> _navigatorItems = [];
    [ObservableProperty] private NavigatorNode? _selectedNavigatorItem;
    [ObservableProperty] private object? _inspector;
    [ObservableProperty] private string _searchQuery = string.Empty;

    public NavigatorNode? SelectedNode => _selectedNavigatorItem;
    public AiPanelViewModel Ai => _ai;
    public SearchViewModel Search => _search;
    public EditorViewModel Editor => _editor;
    public SettingsViewModel SettingsVm => _settingsVm;
    public DashboardViewModel Dashboard => _dash;
    public StoryboardViewModel Board => _board;
    public ManuscriptNode? SelectedManuscriptNode => _selectedNavigatorItem?.Node;

    public MainViewModel(
        IProjectRepository repo, ISettingsStore settings, IBackupService backup, IDeepAiCompanionService deepAi,
        NavigatorViewModel nav, EditorViewModel editor, InspectorViewModel inspector,
        CorkboardViewModel cork, OutlinerViewModel outliner, CharactersViewModel chars, WorldViewModel world,
        TimelineViewModel timeline, ResearchViewModel research, AiPanelViewModel ai, DashboardViewModel dash,
        StatsViewModel stats, SettingsViewModel settingsVm, SearchViewModel search, StoryboardViewModel board, IImportExportService? importExport = null)
    {
        _repo = repo; _importExport = importExport; _settings = settings; _backup = backup; _deepAi = deepAi;
        _nav = nav; _editor = editor; _inspectorVm = inspector; _cork = cork; _outliner = outliner; _chars = chars; _world = world;
        _timeline = timeline; _research = research; _ai = ai; _dash = dash; _stats = stats; _settingsVm = settingsVm; _search = search; _board = board;
        Inspector = _inspectorVm;
        UpdateWorkspace();
    }

    public async Task InitializeAsync()
    {
        await _settings.LoadAsync();
        ShowNavigator = _settings.Current.ShowNavigator;
        ShowInspector = _settings.Current.ShowInspector;
        ShowAi = _settings.Current.ShowAiPanel;
        NavigatorWidth = new GridLength(_settings.Current.NavigatorWidth, GridUnitType.Pixel);
        InspectorWidth = new GridLength(_settings.Current.InspectorWidth, GridUnitType.Pixel);
        if (string.IsNullOrWhiteSpace(_settings.Current.ProjectsRootHint))
            _settings.Current.ProjectsRootHint = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ALICE Projekte");
        Directory.CreateDirectory(_settings.Current.ProjectsRootHint);
        // Falls Recent existiert: still öffnen, sonst leeres Demo-Projekt anlegen
        var recent = _settings.Current.RecentProjects.FirstOrDefault(r => File.Exists(r.Path));
        if (recent is not null) await OpenProjectPathAsync(recent.Path);
        else
        {
            Project = StoryTemplates.CreateFromTemplate("three-act", "Mein erster Roman", string.Empty);
            ProjectPath = Path.Combine(_settings.Current.ProjectsRootHint, "Mein erster Roman.alice.json");
            await _repo.SaveAsync(Project, ProjectPath);
            RegisterRecent(ProjectPath, Project.Title);
            RefreshAll();
        }
        StartAutosave();
    }

    private void StartAutosave()
    {
        _autosaveTimer?.Dispose();
        var interval = TimeSpan.FromSeconds(Math.Max(10, _settings.Current.AutosaveIntervalSec));
        _autosaveTimer = new Timer(async _ =>
        {
            if (!_settings.Current.Autosave || Project is null || ProjectPath is null) return;
            try { await _repo.SaveAsync(Project, ProjectPath); SaveState = "Autosave " + DateTime.Now.ToString("HH:mm:ss"); } catch (Exception ex) { StatusText = "Autosave-Fehler: " + ex.Message; }
        }, null, interval, interval);
    }

    private void RegisterRecent(string path, string title)
    {
        var r = _settings.Current.RecentProjects.FirstOrDefault(x => x.Path == path);
        if (r is null) _settings.Current.RecentProjects.Insert(0, new RecentProject { Path = path, Title = title });
        else { r.LastOpened = DateTime.UtcNow; r.Title = title; }
        _settings.Current.RecentProjects = _settings.Current.RecentProjects.OrderByDescending(x => x.LastOpened).Take(10).ToList();
        _ = _settings.SaveAsync();
    }

    public async Task OpenProjectPathAsync(string path)
    {
        var p = await _repo.LoadAsync(path);
        if (p is null) { StatusText = "Projekt konnte nicht geöffnet werden."; return; }
        Project = p; ProjectPath = path;
        RegisterRecent(path, p.Title);
        RefreshAll();
        StatusText = $"Projekt '{p.Title}' geöffnet.";
    }

    private void RefreshAll()
    {
        if (Project is null) return;
        ProjectTitle = Project.Title;
        TotalWords = Project.TotalWords;
        SaveState = "Gespeichert";
        RefreshNavigator();
        RefreshInspector();
        UpdateWorkspace();
        _editor.Refresh(Project, SelectedManuscriptNode);
        _cork.Refresh(Project);
        _outliner.Refresh(Project);
        _chars.Refresh(Project);
        _world.Refresh(Project);
        _timeline.Refresh(Project);
        _research.Refresh(Project);
        _ai.Refresh(Project);
        _dash.Refresh(Project);
        _stats.Refresh(Project);
        _board.Refresh(Project);
    }

    private void RefreshNavigator()
    {
        NavigatorItems.Clear();
        if (Project is null) return;
        foreach (var root in Project.Roots) NavigatorItems.Add(BuildTree(root));
    }

    private NavigatorNode BuildTree(ManuscriptNode node)
    {
        var vm = new NavigatorNode { Node = node };
        foreach (var c in Project!.ChildrenOf(node.Id)) vm.Children.Add(BuildTree(c));
        return vm;
    }

    partial void OnSelectedNavigatorItemChanged(NavigatorNode? value)
    {
        RefreshInspector();
        _editor.Refresh(Project, SelectedManuscriptNode);
    }

    private void RefreshInspector()
    {
        _inspectorVm.Refresh(Project, SelectedManuscriptNode);
        Inspector = _inspectorVm;
    }

    partial void OnModeIndexChanged(int value) => UpdateWorkspace();

    private void UpdateWorkspace()
    {
        CurrentWorkspace = ModeIndex switch
        {
            1 => _outliner,
            2 => _cork,
            3 => _chars,
            4 => _world,
            5 => _timeline,
            6 => _research,
            7 => _dash,
            8 => _stats,
            9 => _settingsVm,
            10 => _board,
            _ => _editor
        };
        ShowEditorBar = CurrentWorkspace is not StoryboardViewModel;
        StatusText = "Ansicht: " + CurrentWorkspace.GetType().Name.Replace("ViewModel", "");
    }

    // === Commands ===
    [RelayCommand]
    private async Task NewProject()
    {
        var title = "Neues Projekt " + DateTime.Now.ToString("HHmmss");
        var project = StoryTemplates.CreateFromTemplate("blank", title, string.Empty);
        var path = Path.Combine(_settings.Current.ProjectsRootHint, title + ".alice.json");
        await _repo.SaveAsync(project, path);
        Project = project; ProjectPath = path; RegisterRecent(path, title); RefreshAll();
        StatusText = "Neues Projekt erstellt.";
    }

    [RelayCommand]
    private async Task ExportProject(object? formatParam)
    {
        if (Project is null || _importExport is null) { StatusText = "Export nicht verfügbar."; return; }
        var format = formatParam switch { string s when Enum.TryParse<ExportFormat>(s, out var f) => f, ExportFormat f2 => f2, _ => ExportFormat.Pdf };
        var (filter, ext) = format switch { ExportFormat.Pdf => ("PDF (*.pdf)|*.pdf", ".pdf"), ExportFormat.Docx => ("Word (*.docx)|*.docx", ".docx"), ExportFormat.Epub => ("EPUB (*.epub)|*.epub", ".epub"), ExportFormat.Markdown => ("Markdown (*.md)|*.md", ".md"), _ => ("Text (*.txt)|*.txt", ".txt") };
        var safe = string.Concat(Project.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = filter, FileName = safe + ext, InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
        if (dlg.ShowDialog() != true) return;
        try { await _importExport.ExportAsync(Project, dlg.FileName, format); StatusText = "Exportiert: " + dlg.FileName; }
        catch (Exception ex) { StatusText = "Export-Fehler: " + ex.Message; }
    }

    [RelayCommand]
    private async Task ImportProject()
    {
        if (_importExport is null) { StatusText = "Import nicht verfügbar."; return; }
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Text/Markdown (*.txt;*.md)|*.txt;*.md|Word (*.docx)|*.docx|RTF (*.rtf)|*.rtf|Alle Dateien|*.*" };
        if (dlg.ShowDialog() != true) return;
        var format = Path.GetExtension(dlg.FileName).ToLowerInvariant() switch { ".docx" => ImportFormat.Docx, ".rtf" => ImportFormat.Rtf, ".md" => ImportFormat.Markdown, _ => ImportFormat.Txt };
        try
        {
            var imported = await _importExport.ImportAsync(dlg.FileName, format);
            var path = Path.Combine(_settings.Current.ProjectsRootHint, imported.Title + ".alice.json");
            await _repo.SaveAsync(imported, path);
            Project = imported; ProjectPath = path; RegisterRecent(path, imported.Title); RefreshAll();
            StatusText = "Importiert: " + dlg.FileName;
        }
        catch (Exception ex) { StatusText = "Import-Fehler: " + ex.Message; }
    }

    [RelayCommand]
    private async Task OpenProject()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "ALICE Projekt (*.alice.json)|*.alice.json|Alle Dateien|*.*", InitialDirectory = _settings.Current.ProjectsRootHint };
        if (dlg.ShowDialog() == true) await OpenProjectPathAsync(dlg.FileName);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (Project is null || ProjectPath is null) return;
        await _repo.SaveAsync(Project, ProjectPath);
        SaveState = "Gespeichert " + DateTime.Now.ToString("HH:mm:ss");
        RegisterRecent(ProjectPath, Project.Title);
        TotalWords = Project.TotalWords;
        StatusText = "Projekt gespeichert.";
    }

    [RelayCommand]
    private async Task DuplicateProject()
    {
        if (Project is null || ProjectPath is null) return;
        var copy = new AliceProject
        {
            Title = Project.Title + " (Kopie)", Author = Project.Author, Synopsis = Project.Synopsis, Genre = Project.Genre,
            TargetWordCount = Project.TargetWordCount,
            Nodes = System.Text.Json.JsonSerializer.Deserialize<List<ManuscriptNode>>(System.Text.Json.JsonSerializer.Serialize(Project.Nodes)) ?? [],
            Characters = System.Text.Json.JsonSerializer.Deserialize<List<Character>>(System.Text.Json.JsonSerializer.Serialize(Project.Characters)) ?? [],
            WorldEntries = System.Text.Json.JsonSerializer.Deserialize<List<WorldEntry>>(System.Text.Json.JsonSerializer.Serialize(Project.WorldEntries)) ?? [],
            TimelineEvents = System.Text.Json.JsonSerializer.Deserialize<List<TimelineEvent>>(System.Text.Json.JsonSerializer.Serialize(Project.TimelineEvents)) ?? [],
            Research = System.Text.Json.JsonSerializer.Deserialize<List<ResearchItem>>(System.Text.Json.JsonSerializer.Serialize(Project.Research)) ?? [],
        };
        var path = Path.Combine(_settings.Current.ProjectsRootHint, copy.Title + ".alice.json");
        await _repo.SaveAsync(copy, path);
        StatusText = "Projekt dupliziert: " + path;
    }

    [RelayCommand]
    private void ArchiveProject()
    {
        if (ProjectPath is null) return;
        var dir = Path.Combine(_settings.Current.ProjectsRootHint, "Archiv");
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, Path.GetFileName(ProjectPath));
        File.Copy(ProjectPath, dest, overwrite: true);
        StatusText = "Projekt archiviert nach " + dest;
    }

    [RelayCommand]
    private void DeleteProject()
    {
        if (ProjectPath is null) return;
        if (System.Windows.MessageBox.Show($"Projekt '{Project?.Title}' wirklich löschen?\n{ProjectPath}\n(Auto-Backup wird vorher erstellt.)", "Löschen bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (Project is not null) _ = _backup.CreateBackupAsync(Project, ProjectPath);
        File.Delete(ProjectPath);
        StatusText = "Projekt gelöscht.";
        _ = NewProject();
    }

    [RelayCommand]
    private async Task BackupNow()
    {
        if (Project is null || ProjectPath is null) return;
        var f = await _backup.CreateBackupAsync(Project, ProjectPath);
        StatusText = "Backup erstellt: " + f;
    }

    [RelayCommand]
    private async Task Restore()
    {
        if (ProjectPath is null) return;
        var backups = await _backup.ListBackupsAsync(ProjectPath);
        if (backups.Count == 0) { StatusText = "Keine Backups gefunden."; return; }
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Backup (*.alice.json)|*.alice.json", InitialDirectory = _repo.BackupDirFor(ProjectPath) };
        if (dlg.ShowDialog() == true)
        {
            await _backup.RestoreAsync(dlg.FileName, ProjectPath);
            await OpenProjectPathAsync(ProjectPath);
            StatusText = "Backup wiederhergestellt.";
        }
    }

    [RelayCommand] private void Exit() => System.Windows.Application.Current.Shutdown();
    [RelayCommand] private void Undo() => _editor.Undo();
    [RelayCommand] private void Redo() => _editor.Redo();
    [RelayCommand] private void ShowSearch() { ModeIndex = 0; StatusText = "Suche: tragen Sie unten einen Suchbegriff ein (Shift+F für weitere Optionen)."; }
    [RelayCommand] private void ShowReplace() => _editor.OpenReplace();
    [RelayCommand] private void ToggleNavigator() { ShowNavigator = !ShowNavigator; _settings.Current.ShowNavigator = ShowNavigator; }
    [RelayCommand] private void ToggleInspector() { ShowInspector = !ShowInspector; _settings.Current.ShowInspector = ShowInspector; }
    [RelayCommand] private void ToggleAi() { ShowAi = !ShowAi; _settings.Current.ShowAiPanel = ShowAi; }
    [RelayCommand] private void ToggleFocus() { _settings.Current.FocusMode = !_settings.Current.FocusMode; ShowNavigator = !_settings.Current.FocusMode; ShowInspector = !_settings.Current.FocusMode; StatusText = _settings.Current.FocusMode ? "Fokusmodus an." : "Fokusmodus aus."; }
    [RelayCommand] private void ToggleTypewriter() { _settings.Current.TypewriterMode = !_settings.Current.TypewriterMode; StatusText = "Typewriter " + (_settings.Current.TypewriterMode ? "an" : "aus"); }
    [RelayCommand] private void NewSnapshot() => _editor.CreateSnapshot();
    [RelayCommand] private void ShowEditor() => ModeIndex = 0;
    [RelayCommand] private void ShowDashboard() => ModeIndex = 7;
    [RelayCommand] private void ShowCorkboard() => ModeIndex = 2;
    [RelayCommand] private void ShowOutliner() => ModeIndex = 1;
    [RelayCommand] private void ShowCharacters() => ModeIndex = 3;
    [RelayCommand] private void ShowWorld() => ModeIndex = 4;
    [RelayCommand] private void ShowTimeline() => ModeIndex = 5;
    [RelayCommand] private void ShowResearch() => ModeIndex = 6;
    [RelayCommand] private void ShowStats() => ModeIndex = 8;
    [RelayCommand] private void ShowSettings() => ModeIndex = 9;
    [RelayCommand] private void ShowStoryboard() => ModeIndex = 10;

    [RelayCommand]
    private void LaunchDeepAi()
    {
        try { _deepAi.LaunchDeepAi(); StatusText = $"DeepAI Code gestartet (Modell: {_settings.Current.DeepAiModel})."; }
        catch (Exception ex) { StatusText = "DeepAI-Start: " + ex.Message; }
    }

    [RelayCommand]
    private async Task AnalyzeDeepAi()
    {
        if (Project is null) return;
        // Kontext zusammenstellen: Auswahl / Szene / Kapitel
        var node = SelectedManuscriptNode;
        var context = node is null ? Project.Synopsis : $"{node.Title}\n\n{node.Content}\n\nSynopsis: {node.Synopsis}";
        if (Project.Characters.Count > 0) context += "\n\nFiguren:\n" + string.Join("\n", Project.Characters.Select(c => $"- {c.Name}: {c.Description}"));
        var model = _settings.Current.DeepAiModel;
        var prompt = $"[DeepAI-Modell: {model} – bitte in DeepAI Code dieses Modell auswählen]\n" + _deepAi.BuildStructuredPrompt("Analysiere Text auf Konsistenz, Stil und Plot-Fehler. Antworte mit konkreten Punkten.", context);
        // Server erkannt?
        var ok = _deepAi.IsServerReachable(out var detail);
        try
        {
            System.Windows.Clipboard.SetText(prompt);
            StatusText = "Prompt in Zwischenablage kopiert. " + detail + " DeepAI Code wird geöffnet (Begleitworkflow).";
            _deepAi.LaunchDeepAi();
            // Kontextpaket ablegen
            var pkg = Path.Combine(Path.GetTempPath(), $"ALICE-Kontext-{DateTime.Now:yyyyMMdd-HHmmss}.md");
            await _deepAi.ExportContextPackageAsync(Project, pkg);
            StatusText += " Kontextpaket: " + pkg;
        }
        catch (Exception ex) { StatusText = "DeepAI-Aktion: " + ex.Message + " | " + detail; }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void CopyPrompt()
    {
        try
        {
            var t = SelectedManuscriptNode?.Content ?? "Kein Text ausgewählt.";
            System.Windows.Clipboard.SetText(t);
            StatusText = "Auswahl in Zwischenablage kopiert.";
        }
        catch (Exception ex) { StatusText = ex.Message; }
    }

    [RelayCommand]
    private void About() => System.Windows.MessageBox.Show("ALICE — Advanced Literary & Creative Intelligence Studio\nVersion 1.0.0\n\nLocal-First Schreibstudio.\nDeepAI-Code-Anbindung: Begleitworkflow (Start + Clipboard + Kontextpaket) — kein Auth-Bypass.\n\nYour world. Your words. Your story.", "Über ALICE", MessageBoxButton.OK, MessageBoxImage.Information);

    public void AddNode(NodeKind kind)
    {
        if (Project is null) return;
        var parent = SelectedManuscriptNode;
        var node = new ManuscriptNode { Title = kind switch { NodeKind.Chapter => "Neues Kapitel", NodeKind.Scene => "Neue Szene", _ => "Neues Dokument" }, Kind = kind, ParentId = parent?.Id, SortOrder = Project.Nodes.Count(n => n.ParentId == parent?.Id) };
        if (kind == NodeKind.Chapter && parent is not null) node.ParentId = null; // Kapitel = Root
        Project.Nodes.Add(node);
        RefreshNavigator();
        StatusText = "Knoten hinzugefügt: " + node.Title;
    }

    [RelayCommand] private void AddChapter() => AddNode(NodeKind.Chapter);
    [RelayCommand] private void AddScene() => AddNode(NodeKind.Scene);

    [RelayCommand]
    private void DuplicateNode()
    {
        if (Project is null || SelectedManuscriptNode is null) return;
        var src = SelectedManuscriptNode;
        var copy = new ManuscriptNode { Title = src.Title + " (Kopie)", Kind = src.Kind, ParentId = src.ParentId, Content = src.Content, Synopsis = src.Synopsis, Status = src.Status, TargetWordCount = src.TargetWordCount, SortOrder = src.SortOrder + 1, Color = src.Color };
        Project.Nodes.Add(copy);
        RefreshNavigator();
    }

    [RelayCommand]
    private void DeleteNode()
    {
        if (Project is null || SelectedManuscriptNode is null) return;
        if (System.Windows.MessageBox.Show($"'{SelectedManuscriptNode.Title}' löschen?", "Löschen", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        // Rekursiv löschen
        var toDelete = new List<Guid> { SelectedManuscriptNode.Id };
        var stack = new Stack<Guid>(toDelete);
        while (stack.Count > 0) { var id = stack.Pop(); foreach (var c in Project.ChildrenOf(id)) stack.Push(c.Id); toDelete.AddRange(Project.ChildrenOf(id).Select(c => c.Id)); }
        foreach (var id in toDelete.Distinct()) Project.Nodes.RemoveAll(n => n.Id == id);
        RefreshNavigator();
    }
}

/// <summary>Baum-Knoten für Navigator.</summary>
public sealed class NavigatorNode
{
    public ManuscriptNode Node { get; set; } = new();
    public string Title => Node.Title;
    public ObservableCollection<NavigatorNode> Children { get; } = [];
}
