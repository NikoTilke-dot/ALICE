using System.Collections.ObjectModel;
using Alice.Core.Enums;
using Alice.Core.Models;
using Alice.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Alice.App.ViewModels;

public partial class NavigatorViewModel : ObservableObject { public void Refresh(AliceProject? p) { } public void Dispose() { } }

/// <summary>Editor: ManuscriptNode ↔ Text, Snapshots, Undo/Redo, Find/Replace.</summary>
public partial class EditorViewModel : ObservableObject
{
    private readonly IProjectRepository _repo;
    private AliceProject? _project;
    private ManuscriptNode? _node;
    private string _content = string.Empty;
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();

    public ManuscriptNode? Node => _node;
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private string _nodeTitle = string.Empty;
    [ObservableProperty] private string _synopsis = string.Empty;
    [ObservableProperty] private int _wordCount;
    [ObservableProperty] private int _charCount;
    [ObservableProperty] private int _sentenceCount;
    [ObservableProperty] private int _paragraphCount;
    [ObservableProperty] private int _readingMinutes;
    [ObservableProperty] private string _statusLabel = "—";
    [ObservableProperty] private ObservableCollection<Snapshot> _snapshots = [];
    [ObservableProperty] private string _findText = string.Empty;
    [ObservableProperty] private string _replaceText = string.Empty;
    [ObservableProperty] private string _findResult = string.Empty;
    [ObservableProperty] private string _editorFont = "Georgia";
    [ObservableProperty] private string _editorFontSize = "14";

    public EditorViewModel(IProjectRepository repo) => _repo = repo;

    public void Refresh(AliceProject? project, ManuscriptNode? node)
    {
        _project = project; _node = node;
        if (node is not null)
        {
            Text = node.Content ?? string.Empty; _content = Text;
            NodeTitle = node.Title; Synopsis = node.Synopsis ?? string.Empty;
            StatusLabel = node.Status.ToString();
            _snapshots = new ObservableCollection<Snapshot>(node.Snapshots.OrderByDescending(s => s.CreatedAt));
            UpdateCounts();
        }
        else { Text = string.Empty; NodeTitle = "—"; StatusLabel = "—"; Synopsis = string.Empty; UpdateCounts(); }
    }

    partial void OnTextChanged(string value)
    {
        if (_node is not null && _node.Content != value)
        {
            // Debounce-ähnlich: bei jeder Änderung speichern + zählen
            _node.Content = value; _node.UpdatedAt = DateTime.UtcNow;
            if (_project is not null && value.Length - _content.Length > 0) _undo.Push(_content);
            _content = value; _redo.Clear();
            UpdateCounts();
        }
    }

    partial void OnNodeTitleChanged(string value) { if (_node is not null) _node.Title = value; }
    partial void OnSynopsisChanged(string value) { if (_node is not null) _node.Synopsis = value; }

    private void UpdateCounts()
    {
        WordCount = WordCounter.CountWords(Text); CharCount = Text.Length;
        SentenceCount = WordCounter.CountSentences(Text); ParagraphCount = WordCounter.CountParagraphs(Text);
        ReadingMinutes = WordCounter.ReadingMinutes(Text);
    }

    public void Undo() { if (_undo.Count > 0 && _node is not null) { _redo.Push(Text); Text = _undo.Pop(); _node.Content = Text; UpdateCounts(); } }
    public void Redo() { if (_redo.Count > 0 && _node is not null) { _undo.Push(Text); Text = _redo.Pop(); _node.Content = Text; UpdateCounts(); } }
    public void OpenReplace() { FindResult = "Ersetzen-Felder im Editor unten: Suche + Ersetze + 'Ersetzen'."; }

    [RelayCommand]
    private void DoFind()
    {
        if (string.IsNullOrWhiteSpace(FindText) || Text.Length == 0) { FindResult = "Nichts zu suchen."; return; }
        var count = Text.Split(new[] { FindText }, StringSplitOptions.None).Length - 1;
        FindResult = $"{count} Treffer für „{FindText}“";
    }

    [RelayCommand]
    private void DoReplace()
    {
        if (string.IsNullOrWhiteSpace(FindText)) return;
        Text = Text.Replace(FindText, ReplaceText ?? string.Empty, StringComparison.Ordinal);
        FindResult = "Ersetzt.";
        UpdateCounts();
    }

    public void CreateSnapshot()
    {
        if (_node is null) return;
        var s = new Snapshot { Name = "Snapshot " + DateTime.Now.ToString("HH:mm"), Content = Text };
        _node.Snapshots.Add(s);
        Snapshots = new ObservableCollection<Snapshot>(_node.Snapshots.OrderByDescending(x => x.CreatedAt));
    }

    [RelayCommand]
    private void RestoreSnapshot(Snapshot? s)
    {
        if (s is null || _node is null) return;
        if (System.Windows.MessageBox.Show($"Snapshot „{s.Name}“ wiederherstellen? Aktueller Text bleibt als Snapshot erhalten.", "Wiederherstellen", System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes) return;
        CreateSnapshot(); // aktuellen Stand sichern
        Text = s.Content; _node.Content = Text; UpdateCounts();
    }
}

/// <summary>Inspektor: Metadaten des Selektion.</summary>
public partial class InspectorViewModel : ObservableObject
{
    private AliceProject? _project;
    private ManuscriptNode? _node;
    [ObservableProperty] private string _title = "—";
    [ObservableProperty] private string _kind = "—";
    [ObservableProperty] private string _status = "—";
    [ObservableProperty] private int _words;
    [ObservableProperty] private int _target;
    [ObservableProperty] private string _synopsis = string.Empty;
    [ObservableProperty] private string _tags = string.Empty;
    [ObservableProperty] private string _pov = string.Empty;
    [ObservableProperty] private string _location = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    public void Refresh(AliceProject? project, ManuscriptNode? node)
    {
        _project = project; _node = node;
        if (node is null) { Title = "Nichts ausgewählt"; return; }
        Title = node.Title; Kind = node.Kind.ToString(); Status = node.Status.ToString();
        Words = node.WordCount; Target = node.TargetWordCount;
        Synopsis = node.Synopsis ?? string.Empty; Tags = node.Tags ?? string.Empty; Notes = node.Notes ?? string.Empty;
        Location = node.Location ?? string.Empty;
        Pov = node.PovCharacterId is null ? "—" : project?.Characters.FirstOrDefault(c => c.Id.ToString() == node.PovCharacterId)?.Name ?? "—";
    }
}

/// <summary>Corkboard: Karten-Ansicht.</summary>
public partial class CorkboardViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<ManuscriptNode> _cards = [];
    [ObservableProperty] private string _filter = string.Empty;
    public void Refresh(AliceProject? project)
    {
        Cards.Clear();
        if (project is null) return;
        foreach (var n in project.Nodes.Where(x => x.Kind == NodeKind.Scene || x.Kind == NodeKind.Chapter).OrderBy(x => x.SortOrder)) Cards.Add(n);
    }
    [RelayCommand] private void ApplyFilter() { if (string.IsNullOrWhiteSpace(Filter)) return; }
}

/// <summary>Outliner: Tabellenansicht.</summary>
public partial class OutlinerViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<ManuscriptNode> _rows = [];
    public void Refresh(AliceProject? project)
    {
        Rows.Clear(); if (project is null) return;
        foreach (var n in project.Nodes.OrderBy(x => x.SortOrder)) Rows.Add(n);
    }
}

/// <summary>Figuren.</summary>
public partial class CharactersViewModel : ObservableObject
{
    private AliceProject? _project;
    [ObservableProperty] private ObservableCollection<Character> _characters = [];
    [ObservableProperty] private Character? _selected;
    [ObservableProperty] private string _search = string.Empty;
    public void Refresh(AliceProject? project)
    {
        _project = project; Characters.Clear(); if (project is null) return;
        foreach (var c in project.Characters) Characters.Add(c);
    }
    [RelayCommand] private void NewCharacter() { if (_project is null) return; var c = new Character(); _project.Characters.Add(c); Characters.Add(c); Selected = c; }
    [RelayCommand] private void DeleteCharacter() { if (_project is null || Selected is null) return; _project.Characters.Remove(Selected); Characters.Remove(Selected); }
}

/// <summary>Weltbau.</summary>
public partial class WorldViewModel : ObservableObject
{
    private AliceProject? _project;
    [ObservableProperty] private ObservableCollection<WorldEntry> _entries = [];
    [ObservableProperty] private WorldEntry? _selected;
    public void Refresh(AliceProject? project) { _project = project; Entries.Clear(); if (project is null) return; foreach (var w in project.WorldEntries) Entries.Add(w); }
    [RelayCommand] private void NewEntry() { if (_project is null) return; var w = new WorldEntry(); _project.WorldEntries.Add(w); Entries.Add(w); Selected = w; }
    [RelayCommand] private void DeleteEntry() { if (_project is null || Selected is null) return; _project.WorldEntries.Remove(Selected); Entries.Remove(Selected); }
}

/// <summary>Zeitlinie.</summary>
public partial class TimelineViewModel : ObservableObject
{
    private AliceProject? _project;
    [ObservableProperty] private ObservableCollection<TimelineEvent> _events = [];
    public void Refresh(AliceProject? project) { _project = project; Events.Clear(); if (project is null) return; foreach (var e in project.TimelineEvents.OrderBy(x => x.Date ?? DateTime.MaxValue)) Events.Add(e); }
    [RelayCommand] private void NewEvent() { if (_project is null) return; var e = new TimelineEvent(); _project.TimelineEvents.Add(e); Events.Add(e); }
    [RelayCommand] private void DeleteEvent(TimelineEvent? e) { if (_project is null || e is null) return; _project.TimelineEvents.Remove(e); Events.Remove(e); }
}

/// <summary>Recherche.</summary>
public partial class ResearchViewModel : ObservableObject
{
    private AliceProject? _project;
    [ObservableProperty] private ObservableCollection<ResearchItem> _items = [];
    public void Refresh(AliceProject? project) { _project = project; Items.Clear(); if (project is null) return; foreach (var r in project.Research) Items.Add(r); }
    [RelayCommand] private void NewItem() { if (_project is null) return; var r = new ResearchItem(); _project.Research.Add(r); Items.Add(r); }
    [RelayCommand] private void DeleteItem(ResearchItem? r) { if (_project is null || r is null) return; _project.Research.Remove(r); Items.Remove(r); }
}

/// <summary>Dashboard.</summary>
public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty] private string _projectTitle = "—";
    [ObservableProperty] private string _synopsis = string.Empty;
    [ObservableProperty] private int _totalWords;
    [ObservableProperty] private int _scenes;
    [ObservableProperty] private int _chapters;
    [ObservableProperty] private double _progress;
    public void Refresh(AliceProject? p)
    {
        if (p is null) return;
        ProjectTitle = p.Title; Synopsis = p.Synopsis; TotalWords = p.TotalWords; Scenes = p.TotalScenes; Chapters = p.TotalChapters;
        Progress = p.TargetWordCount <= 0 ? 0 : Math.Min(100, p.TotalWords * 100.0 / p.TargetWordCount);
    }
}

/// <summary>Statistik.</summary>
public partial class StatsViewModel : ObservableObject
{
    private readonly IStatsService _stats;
    [ObservableProperty] private string _summary = "—";
    public StatsViewModel(IStatsService stats) => _stats = stats;
    public void Refresh(AliceProject? p)
    {
        if (p is null) return;
        var s = _stats.Compute(p);
        Summary = $"Wörter: {s.TotalWords:N0} · Zeichen: {s.TotalCharacters:N0} · Szenen: {s.TotalScenes} · Kapitel: {s.TotalChapters} · Fortschritt: {s.ProgressPct}%";
    }
}
