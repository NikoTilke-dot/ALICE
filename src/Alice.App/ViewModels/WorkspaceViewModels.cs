using System.Collections.ObjectModel;
using System.Windows.Documents;
using System.Windows.Controls;
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
    private bool _documentSyncing;
    private System.Windows.Threading.DispatcherTimer? _syncTimer;
    private System.Windows.Documents.FlowDocument _document = new();

    public ManuscriptNode? Node => _node;
    /// <summary>Das eine Autorenobjekt (seitenbasiertes FlowDocument). Der Klartext unten ist nur abgeleitet (Zähler/Export/AI/Suche).</summary>
    public System.Windows.Documents.FlowDocument Document
    {
        get => _document;
        private set { _document = value; OnPropertyChanged(); }
    }
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
            // Dokument laden: XAML-Entwurf (Formatierung) oder Klartext-Fallback.
            _documentSyncing = true;
            FlowDocument doc;
            try
            {
                // Kapitel-Struktur (Buch): Titelseite (generiert) + Seitenumbruch + Schreibseiten (Inhalt).
                doc = FlowDocumentTools.CompileChapterDocument(node);
                Document = doc;
                Text = FlowDocumentTools.ToPlainText(doc); _content = Text;
            }
            finally { _documentSyncing = false; }
            HookDocument(doc);
            NodeTitle = node.Title; Synopsis = node.Synopsis ?? string.Empty;
            StatusLabel = node.Status.ToString();
            _snapshots = new ObservableCollection<Snapshot>(node.Snapshots.OrderByDescending(s => s.CreatedAt));
            UpdateCounts();
        }
        else { Text = string.Empty; NodeTitle = "—"; StatusLabel = "—"; Synopsis = string.Empty; UpdateCounts(); }
    }

    /// <summary>Layout-Hook: gesetzt vom MainViewModel (weisses Papier, schwarze Schrift, A4-Groesse/Padding). Wird bei JEDER neuen FlowDocument-Erzeugung aufgerufen - so bleibt das Layout auch nach Refresh()/Snapshot-Restore stabil.</summary>
    public Action<FlowDocument>? LayoutDocument;

    /// <summary>Wird von "+ Kapitel"/"+ Szene" gesetzt: der Cursor soll auf der ersten Schreibseite landen.</summary>
    public bool RequestFocusBody { get; set; }

    /// <summary>An das FlowDocument haengen: nur Font-Grundwerte schreiben (TextChanged kommt ueber die RichTextBox des Viewers).</summary>
    public void HookDocument(FlowDocument doc)
    {
        if (_documentSyncing) return;
        if (!string.IsNullOrWhiteSpace(EditorFont))
        {
            try { doc.FontFamily = new System.Windows.Media.FontFamily(EditorFont); } catch { }
            if (double.TryParse(EditorFontSize, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fs) && fs > 0)
                doc.FontSize = fs;
        }
        LayoutDocument?.Invoke(doc);
    }

    /// <summary>Der Viewer meldet Textaenderungen aus der fokussierten RichTextBox. Debounced (250 ms) aus dem FlowDocument ziehen.</summary>
    public void NotifyDocumentDirty()
    {
        if (_documentSyncing || _node is null) return;
        if (_syncTimer is null)
        {
            _syncTimer = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(250) };
            _syncTimer.Tick += (_, __) =>
            {
                _syncTimer!.Stop();
                var plain = FlowDocumentTools.ToPlainText(Document);
                if (plain != Text) Text = plain;
                // Kapitel-Titelzeile bearbeitet? -> node.Title uebernehmen (Rename-Sync zur Titelseite).
                if (_node is not null)
                {
                    var t = FlowDocumentTools.ExtractEditedChapterTitle(Document, _node.Title);
                    if (t is not null && t != _node.Title) { _node.Title = t; _node.UpdatedAt = DateTime.UtcNow; NodeTitle = t; }
                }
            };
        }
        _syncTimer.Stop(); _syncTimer.Start();
    }

    partial void OnTextChanged(string value)
    {
        if (_node is not null && !_documentSyncing)
        {
            // Debounce-ähnlich: bei jeder Änderung speichern + zählen.
            // Content (Klartext) = Source of Truth; ContentRich = XAML-Entwurf (Formatierung).
            _node.Content = value; _node.UpdatedAt = DateTime.UtcNow;
            _node.ContentRich = FlowDocumentTools.ToXaml(Document);
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

    /// <summary>Undo-Punkt setzen (z. B. vor Auto-Korrekturen): aktueller Fließtext auf den ALICE-Undo-Stack.
    /// Damit sind Auto-Korrekturen über das ALICE-Rückgängig (Menü) rückgängig zu machen.</summary>
    public void MarkUndoPoint()
    {
        try
        {
            var plain = FlowDocumentTools.ToPlainText(Document);
            if (plain != _content) { _undo.Push(_content); _content = plain; _redo.Clear(); }
        }
        catch { }
    }

    /// <summary>Externe Undo-Anfrage (z. B. Menü): ALICE-Stack zurückrollen (Text + Dokument neu aufbauen),
    /// sonst Meldung, dass die Formatierungspfade über den RichTextBox-Editor rückgängig laufen (Ctrl+Z/Ctrl+Y).</summary>
    public void UndoRequest()
    {
        if (_undo.Count > 0)
        {
            var t = _undo.Pop();
            _redo.Push(_content);
            LoadPlainTextIntoDocument(t);
            StatusLabel = "Letzte Korrektur/Änderung rückgängig.";
        }
        else StatusLabel = "Rückgängig: Ctrl+Z im Editor (RichText)";
    }
    public void RedoRequest() { StatusLabel = "Wiederherstellen: Ctrl+Y im Editor (RichText)"; }
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
        // Absatzbasiert im FlowDocument ersetzen (Formatierung um die Treffer bleibt).
        var replaced = FlowDocumentTools.ReplaceAll(Document, FindText, ReplaceText ?? string.Empty, caseSensitive: true);
        var plain = FlowDocumentTools.ToPlainText(Document);
        Text = plain; _node?.UpdatedAt = DateTime.UtcNow;
        if (_node is not null) { _node.Content = plain; _node.ContentRich = FlowDocumentTools.ToXaml(Document); }
        FindResult = replaced > 0 ? $"{replaced}× ersetzt." : "Kein Treffer.";
        UpdateCounts();
    }

    /// <summary>Snapshot wiederherstellen: Kapitel-Dokument mit dem Klartext als Schreibseiten neu aufbauen (Formatierung entfällt bei Snapshots, Titelseite bleibt).</summary>
    internal void LoadPlainTextIntoDocument(string plain, bool setRich = false)
    {
        FlowDocument? hookDoc = null;
        _documentSyncing = true;
        try
        {
            var doc = _node is not null ? FlowDocumentTools.CompileChapterDocumentWithBody(_node, FlowDocumentTools.FromPlainText(plain)) : FlowDocumentTools.FromPlainText(plain);
            Document = doc;
            Text = FlowDocumentTools.ToPlainText(doc);
            if (_node is not null) { _node.Content = Text; _node.ContentRich = FlowDocumentTools.ToXaml(doc); }
            hookDoc = doc;
        }
        finally { _documentSyncing = false; }
        if (hookDoc is not null) HookDocument(hookDoc);
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
        LoadPlainTextIntoDocument(s.Content);
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
    // Rueckschreiben: Titel/Synopsis im Inspektor bearbeiten -> direkt am Knoten (Explorer/ Kapitel-Heading synchronisieren sich ueber Refresh).
    partial void OnTitleChanged(string value)
    {
        if (_node is not null && !string.IsNullOrWhiteSpace(value) && value != _node.Title)
        {
            _node.Title = value;
            _node.UpdatedAt = DateTime.UtcNow;
        }
    }
    partial void OnSynopsisChanged(string value)
    {
        if (_node is not null && value != (_node.Synopsis ?? string.Empty))
        {
            _node.Synopsis = value;
            _node.UpdatedAt = DateTime.UtcNow;
        }
    }

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
