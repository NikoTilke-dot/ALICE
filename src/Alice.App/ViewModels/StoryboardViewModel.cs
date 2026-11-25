using System.Collections.ObjectModel;
using System.Text.Json;
using Alice.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Alice.App.ViewModels;

/// <summary>Node-Wrapper für Observable-Updates (Position/Größe/Status).</summary>
public partial class BoardNodeVm : ObservableObject
{
    public BoardNode Model { get; }
    public BoardNodeVm(BoardNode m) { Model = m; Sync(); }
    public Guid Id => Model.Id;
    [ObservableProperty] private double _x; [ObservableProperty] private double _y;
    [ObservableProperty] private double _width; [ObservableProperty] private double _height;
    [ObservableProperty] private bool _isSelected; [ObservableProperty] private bool _isHighlighted;
    [ObservableProperty] private bool _isVisible = true; [ObservableProperty] private bool _isCollapsed;
    [ObservableProperty] private string _title = ""; [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _accent = "#5B8DEF"; [ObservableProperty] private string _glyph = "";
    [ObservableProperty] private string _typeLabel = ""; [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _category = ""; [ObservableProperty] private string _color = "#1A1B2A";
    [ObservableProperty] private string _tags = ""; [ObservableProperty] private string _symbol = "";
    [ObservableProperty] private BoardNodeType _nodeType; [ObservableProperty] private int _zIndex;
    public double CenterX => Model.CenterX; public double CenterY => Model.CenterY; public Guid? GroupId => Model.GroupId;
    public string LinkKindText { get => Model.LinkKind.ToString(); set { if (Enum.TryParse<BoardLinkKind>(value, out var k)) Model.LinkKind = k; } }
    public string LinkId { get => Model.LinkId; set => Model.LinkId = value; }
    public string LinkPreviewText { get; set; } = "—";

    public void Sync()
    {
        X = Model.X; Y = Model.Y; Width = Model.Width; Height = Model.Height;
        Title = Model.Title; Text = Model.Text; NodeType = Model.NodeType;
        Accent = Model.EffectiveAccent; Glyph = Model.EffectiveGlyph;
        TypeLabel = BoardNodeTypes.LabelOf(Model.NodeType);
        Status = Model.Status; Category = Model.Category; Color = Model.Color; Tags = Model.Tags; Symbol = Model.Symbol;
        IsCollapsed = Model.IsCollapsed; ZIndex = Model.ZIndex;
    }
    public void Push() { Model.X = X; Model.Y = Y; Model.Width = Width; Model.Height = Height; Model.Title = Title; Model.Text = Text; Model.NodeType = NodeType; Model.Status = Status; Model.Category = Category; Model.Color = Color; Model.Tags = Tags; Model.Symbol = Symbol; Model.IsCollapsed = IsCollapsed; Model.ZIndex = ZIndex; Model.UpdatedAt = DateTime.UtcNow; }
    partial void OnXChanged(double v) { Model.X = v; } partial void OnYChanged(double v) { Model.Y = v; }
    partial void OnWidthChanged(double v) { Model.Width = v; } partial void OnHeightChanged(double v) { Model.Height = v; }
    partial void OnTitleChanged(string v) { Model.Title = v; Model.UpdatedAt = DateTime.UtcNow; }
    partial void OnTextChanged(string v) { Model.Text = v; Model.UpdatedAt = DateTime.UtcNow; }
    partial void OnStatusChanged(string v) { Model.Status = v; } partial void OnCategoryChanged(string v) { Model.Category = v; }
    partial void OnColorChanged(string v) { Model.Color = v; } partial void OnTagsChanged(string v) { Model.Tags = v; }
    partial void OnSymbolChanged(string v) { Model.Symbol = v; Glyph = Model.EffectiveGlyph; }
    partial void OnNodeTypeChanged(BoardNodeType v) { Model.NodeType = v; Accent = Model.EffectiveAccent; Glyph = Model.EffectiveGlyph; TypeLabel = BoardNodeTypes.LabelOf(v); }
    partial void OnIsCollapsedChanged(bool v) { Model.IsCollapsed = v; }
}

/// <summary>Verbindungs-Wrapper mit dynamischer Geometrie (folgt Node-Bewegungen).</summary>
public partial class BoardConnectionVm : ObservableObject
{
    public BoardConnection Model { get; }
    private readonly Func<Guid, BoardNodeVm?> _findNode;
    public BoardConnectionVm(BoardConnection m, Func<Guid, BoardNodeVm?> find) { Model = m; _findNode = find; RefreshGeometry(); }
    [ObservableProperty] private double _x1; [ObservableProperty] private double _y1;
    [ObservableProperty] private double _x2; [ObservableProperty] private double _y2;
    [ObservableProperty] private double _labelX; [ObservableProperty] private double _labelY;
    [ObservableProperty] private string _label = ""; [ObservableProperty] private string _lineColor = "#4A4D6B";
    [ObservableProperty] private bool _hasArrow = true; [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private BoardConnectionKind _kind; [ObservableProperty] private string _kindLabel = "";
    public void RefreshGeometry()
    {
        var a = _findNode(Model.FromNodeId); var b = _findNode(Model.ToNodeId);
        if (a is null || b is null) return;
        // Linie von rechter Mitte der Quelle zu linker Mitte des Ziels (Leserichtung links→rechts)
        X1 = a.Model.RightX; Y1 = a.Model.MidY; X2 = b.Model.X; Y2 = b.Model.MidY;
        // Falls Ziel links von Quelle: von linker zu rechter Kante (Rückverbindung)
        if (X2 < X1) { X1 = a.Model.X; X2 = b.Model.RightX; }
        LabelX = (X1 + X2) / 2; LabelY = (Y1 + Y2) / 2;
        Label = Model.Label; LineColor = string.IsNullOrWhiteSpace(Model.Color) ? "#4A4D6B" : Model.Color;
        HasArrow = Model.HasArrow; Kind = Model.Kind; KindLabel = BoardConnectionKinds.LabelOf(Model.Kind);
    }
    public void Push() { Model.Label = Label; Model.Color = LineColor; Model.HasArrow = HasArrow; Model.Kind = Kind; }
}

/// <summary>Gruppen-Wrapper.</summary>
public partial class BoardGroupVm : ObservableObject
{
    public BoardGroup Model { get; }
    public BoardGroupVm(BoardGroup m) { Model = m; Sync(); }
    [ObservableProperty] private double _x; [ObservableProperty] private double _y;
    [ObservableProperty] private double _width; [ObservableProperty] private double _height;
    [ObservableProperty] private string _title = ""; [ObservableProperty] private string _color = "#2A2452";
    [ObservableProperty] private bool _isCollapsed; [ObservableProperty] private bool _isVisible = true;
    public void Sync() { X = Model.X; Y = Model.Y; Width = Model.Width; Height = Model.Height; Title = Model.Title; Color = Model.Color; IsCollapsed = Model.IsCollapsed; }
    partial void OnXChanged(double v) { Model.X = v; } partial void OnYChanged(double v) { Model.Y = v; }
    partial void OnWidthChanged(double v) { Model.Width = v; } partial void OnHeightChanged(double v) { Model.Height = v; }
    partial void OnTitleChanged(string v) { Model.Title = v; } partial void OnColorChanged(string v) { Model.Color = v; }
    partial void OnIsCollapsedChanged(bool v) { Model.IsCollapsed = v; }
}

/// <summary>Option für Verknüpfung (Figur/Welt/Manuskript/Zeitlinie).</summary>
public sealed class LinkedOption { public string Id { get; set; } = ""; public string Display { get; set; } = ""; }

/// <summary>Storyboard-Whiteboard: Canvas, Nodes, Verbindungen, Gruppen, Suche, KI-Kontext, Undo/Redo.</summary>
public partial class StoryboardViewModel : ObservableObject
{
    private AliceProject? _project;
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private bool _suspendHistory;
    private BoardNodeVm? _connectSource;

    [ObservableProperty] private ObservableCollection<BoardNodeVm> _nodes = [];
    [ObservableProperty] private ObservableCollection<BoardConnectionVm> _connections = [];
    [ObservableProperty] private ObservableCollection<BoardGroupVm> _groups = [];
    [ObservableProperty] private ObservableCollection<Whiteboard> _boards = [];
    [ObservableProperty] private Whiteboard? _activeBoard;
    [ObservableProperty] private BoardNodeVm? _selectedNode;
    [ObservableProperty] private BoardConnectionVm? _selectedConnection;
    [ObservableProperty] private BoardGroupVm? _selectedGroup;
    [ObservableProperty] private double _zoom = 1.0;
    [ObservableProperty] private double _panX; [ObservableProperty] private double _panY;
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private bool _isHandTool; [ObservableProperty] private bool _isConnectMode;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ObservableCollection<BoardNodeVm> _searchResults = [];
    public bool HasSearchResults => SearchResults.Count > 0;
    [ObservableProperty] private string _filterType = "Alle Typen";
    [ObservableProperty] private string _statusText = "Storyboard bereit. Rechtsklick auf die Fläche für einen neuen Node.";
    [ObservableProperty] private string _kiScope = "Ausgewählte Nodes";
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private ObservableCollection<LinkedOption> _linkOptions = [];
    [ObservableProperty] private string _selectedLinkId = string.Empty;
    public IReadOnlyList<string> ColorPalette { get; } = ["#1A1B2A", "#252646", "#1E3A32", "#3A2A1A", "#33201F", "#2A2452", "#0F2A3A", "#2A0F2E"];
    public IReadOnlyList<string> AccentPalette { get; } = ["#5B8DEF", "#E05C7A", "#2EA8A0", "#8B7CF0", "#F0A35E", "#4ADE80", "#FBBF24", "#F87171", "#38BDF8", "#C084FC"];
    // Für Kontextmenü: Erstellungsposition in Canvas-Koordinaten
    public double PendingX { get; set; } public double PendingY { get; set; }
    public IReadOnlyList<BoardNodeTypes.Info> NodeTypeCatalog => BoardNodeTypes.All;
    public IReadOnlyList<(BoardConnectionKind Kind, string Label)> ConnectionKindCatalog => BoardConnectionKinds.All;
    public int NodeCount => Nodes.Count;
    public string ZoomLabel => $"{Zoom:P0}";

    public void Refresh(AliceProject? project)
    {
        _project = project;
        Boards.Clear();
        if (project is null) { ActiveBoard = null; return; }
        if (project.Whiteboards.Count == 0)
        {
            var wb = new Whiteboard { Title = "Gesamtplot" };
            wb.Nodes.Add(new BoardNode { Title = "Hauptfigur", NodeType = BoardNodeType.Character, Text = "Ziel, Hintergrund, Geheimnis …", X = 80, Y = 80 });
            wb.Nodes.Add(new BoardNode { Title = "Kapitel 1 – Anfang", NodeType = BoardNodeType.Chapter, Text = "Eröffnungsszene …", X = 420, Y = 80 });
            wb.Connections.Add(new BoardConnection { FromNodeId = wb.Nodes[0].Id, ToNodeId = wb.Nodes[1].Id, Label = "erlebt" });
            project.Whiteboards.Add(wb); project.ActiveWhiteboardId = wb.Id;
        }
        LinkOptions.Clear();
        foreach (var c in project.Characters) LinkOptions.Add(new LinkedOption { Id = c.Id.ToString(), Display = "Figur: " + c.Name });
        foreach (var w in project.WorldEntries) LinkOptions.Add(new LinkedOption { Id = w.Id.ToString(), Display = "Welt: " + w.Title });
        foreach (var n in project.Nodes.Where(n => !n.IsFolder)) LinkOptions.Add(new LinkedOption { Id = n.Id.ToString(), Display = "Manuskript: " + n.Title });
        foreach (var t in project.TimelineEvents) LinkOptions.Add(new LinkedOption { Id = t.Id.ToString(), Display = "Zeitlinie: " + t.Title });
        foreach (var b in project.Whiteboards) Boards.Add(b);
        var active = project.Whiteboards.FirstOrDefault(b => b.Id == project.ActiveWhiteboardId) ?? project.Whiteboards[0];
        SetActiveBoard(active);
    }

    private void SetActiveBoard(Whiteboard board)
    {
        if (_project is not null) _project.ActiveWhiteboardId = board.Id;
        _suspendHistory = true;
        ActiveBoard = board;
        Zoom = board.Zoom <= 0 ? 1.0 : board.Zoom; PanX = board.PanX; PanY = board.PanY; ShowGrid = board.ShowGrid;
        Nodes.Clear(); foreach (var n in board.Nodes) Nodes.Add(new BoardNodeVm(n));
        Connections.Clear(); foreach (var c in board.Connections) Connections.Add(new BoardConnectionVm(c, FindNode));
        Groups.Clear(); foreach (var g in board.Groups) Groups.Add(new BoardGroupVm(g));
        SelectedNode = null; SelectedConnection = null; SelectedGroup = null;
        _suspendHistory = false;
        RefreshGeometry(); ApplyFilter(); UpdateStatus();
    }

    public BoardNodeVm? FindNode(Guid id) => Nodes.FirstOrDefault(n => n.Id == id);

    private void UpdateStatus() => StatusText = $"{Nodes.Count} Nodes · {Connections.Count} Verbindungen · {Groups.Count} Gruppen · Zoom {Zoom:P0}";

    // ---------- Undo/Redo (Board-Snapshot als JSON) ----------
    private void PushHistory()
    {
        if (_suspendHistory || ActiveBoard is null) return;
        _undo.Push(JsonSerializer.Serialize(new { Nodes = ActiveBoard.Nodes, Connections = ActiveBoard.Connections, Groups = ActiveBoard.Groups }));
        if (_undo.Count > 100) _undo.Clear();
        _redo.Clear();
    }
    [RelayCommand] public void UndoBoard() { if (_undo.Count == 0 || ActiveBoard is null) return; _redo.Push(JsonSerializer.Serialize(new { Nodes = ActiveBoard.Nodes, Connections = ActiveBoard.Connections, Groups = ActiveBoard.Groups })); RestoreSnapshot(_undo.Pop()); }
    [RelayCommand] public void RedoBoard() { if (_redo.Count == 0 || ActiveBoard is null) return; _undo.Push(JsonSerializer.Serialize(new { Nodes = ActiveBoard.Nodes, Connections = ActiveBoard.Connections, Groups = ActiveBoard.Groups })); RestoreSnapshot(_redo.Pop()); }
    private void RestoreSnapshot(string json)
    {
        _suspendHistory = true;
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        ActiveBoard!.Nodes = JsonSerializer.Deserialize<List<BoardNode>>(doc.GetProperty("Nodes").GetRawText()) ?? [];
        ActiveBoard.Connections = JsonSerializer.Deserialize<List<BoardConnection>>(doc.GetProperty("Connections").GetRawText()) ?? [];
        ActiveBoard.Groups = JsonSerializer.Deserialize<List<BoardGroup>>(doc.GetProperty("Groups").GetRawText()) ?? [];
        // Views neu aufbauen
        Nodes.Clear(); foreach (var n in ActiveBoard.Nodes) Nodes.Add(new BoardNodeVm(n));
        Connections.Clear(); foreach (var c in ActiveBoard.Connections) Connections.Add(new BoardConnectionVm(c, FindNode));
        Groups.Clear(); foreach (var g in ActiveBoard.Groups) Groups.Add(new BoardGroupVm(g));
        SelectedNode = null; SelectedConnection = null;
        _suspendHistory = false; RefreshGeometry(); ApplyFilter(); UpdateStatus();
    }
    public void BeginChange() => PushHistory();
    public void EndChange() { if (ActiveBoard is not null) ActiveBoard.UpdatedAt = DateTime.UtcNow; RefreshGeometry(); UpdateStatus(); }

    // ---------- Canvas ----------
    partial void OnZoomChanged(double v) { if (ActiveBoard is not null) ActiveBoard.Zoom = v; OnPropertyChanged(nameof(ZoomLabel)); }
    partial void OnPanXChanged(double v) { if (ActiveBoard is not null) ActiveBoard.PanX = v; }
    partial void OnPanYChanged(double v) { if (ActiveBoard is not null) ActiveBoard.PanY = v; }
    public void PanBy(double dx, double dy) { PanX += dx; PanY += dy; }
    public void ZoomAt(double factor, double screenX, double screenY, double viewW, double viewH)
    {
        var old = Zoom; var nw = Math.Clamp(old * factor, 0.2, 3.0);
        // Zoom um Cursor: Weltpunkt unter Cursor bleibt fix
        var wx = (screenX - PanX) / old; var wy = (screenY - PanY) / old;
        PanX = screenX - wx * nw; PanY = screenY - wy * nw; Zoom = nw;
    }
    [RelayCommand] public void ZoomIn() => Zoom = Math.Clamp(Zoom * 1.2, 0.2, 3.0);
    [RelayCommand] public void ZoomOut() => Zoom = Math.Clamp(Zoom / 1.2, 0.2, 3.0);
    [RelayCommand] public void ZoomFit()
    {
        if (Nodes.Count == 0) { Zoom = 1; PanX = 40; PanY = 40; return; }
        var minX = Nodes.Min(n => n.X); var minY = Nodes.Min(n => n.Y);
        var maxX = Nodes.Max(n => n.X + n.Width); var maxY = Nodes.Max(n => n.Y + n.Height);
        var w = Math.Max(200, maxX - minX); var h = Math.Max(200, maxY - minY);
        Zoom = Math.Clamp(Math.Min(900 / w, 600 / h), 0.2, 2.0);
        PanX = 40 - minX * Zoom; PanY = 40 - minY * Zoom;
    }
    [RelayCommand] public void ToggleGrid() { ShowGrid = !ShowGrid; if (ActiveBoard is not null) ActiveBoard.ShowGrid = ShowGrid; }
    [RelayCommand] public void ToggleHand() { IsHandTool = !IsHandTool; StatusText = IsHandTool ? "Hand-Werkzeug: Ziehen zum Verschieben der Fläche." : "Auswahl-Werkzeug."; }
    [RelayCommand] public void ToggleConnect() { IsConnectMode = !IsConnectMode; _connectSource = null; StatusText = IsConnectMode ? "Verbinding erstellen: Quell-Node anklicken, dann Ziel-Node." : "Auswahl-Werkzeug."; }

    // ---------- Nodes erstellen ----------
    public BoardNodeVm AddNodeAt(BoardNodeType type, double x, double y, string title = "")
    {
        if (ActiveBoard is null) return null!;
        BeginChange();
        var info = BoardNodeTypes.Of(type);
        var node = new BoardNode
        {
            Title = string.IsNullOrWhiteSpace(title) ? info.Label : title,
            NodeType = type, X = x, Y = y, Width = 220, Height = 120,
            AccentColor = info.Color, Symbol = info.Glyph,
            ZIndex = Nodes.Count + 1,
        };
        ActiveBoard.Nodes.Add(node);
        var vm = new BoardNodeVm(node); Nodes.Add(vm);
        SelectOnly(vm); EndChange();
        return vm;
    }
    [RelayCommand] public void AddNote() => AddNodeAt(BoardNodeType.Note, NextFreePos().x, NextFreePos().y);
    private (double x, double y) NextFreePos() => (80 + (Nodes.Count % 6) * 40, 80 + (Nodes.Count % 4) * 40);
    [RelayCommand] public void AddAtPending(object? type) => AddNodeAt(ParseType(type), PendingX, PendingY);
    private static BoardNodeType ParseType(object? t) => t switch { BoardNodeType b => b, string s when Enum.TryParse<BoardNodeType>(s, out var v) => v, _ => BoardNodeType.Note };

    public BoardNodeVm AddConnectedNode(BoardNodeVm? source, BoardNodeType type)
    {
        if (source is null || ActiveBoard is null) return null!;
        BeginChange();
        var vm = AddNodeAt(type, source.X + source.Width + 60, source.Y);
        var conn = new BoardConnection { FromNodeId = source.Id, ToNodeId = vm.Id };
        ActiveBoard.Connections.Add(conn);
        Connections.Add(new BoardConnectionVm(conn, FindNode));
        EndChange(); return vm;
    }
    [RelayCommand] public void AddConnected(object? type)
    {
        var s = SelectedNode; if (s is null) { StatusText = "Bitte einen Node auswählen."; return; }
        AddConnectedNode(s, ParseType(type));
    }

    [RelayCommand] public void DuplicateSelected()
    {
        var s = SelectedNode; if (s is null || ActiveBoard is null) return;
        BeginChange();
        var src = s.Model;
        var copy = JsonSerializer.Deserialize<BoardNode>(JsonSerializer.Serialize(src))!;
        copy.Id = Guid.NewGuid(); copy.Title = src.Title + " (Kopie)"; copy.X += 30; copy.Y += 30;
        ActiveBoard.Nodes.Add(copy); var vm = new BoardNodeVm(copy); Nodes.Add(vm); SelectOnly(vm); EndChange();
    }
    [RelayCommand] public void DeleteSelected()
    {
        if (SelectedNode is null || ActiveBoard is null) return;
        BeginChange();
        var id = SelectedNode.Id;
        ActiveBoard.Nodes.RemoveAll(n => n.Id == id);
        ActiveBoard.Connections.RemoveAll(c => c.FromNodeId == id || c.ToNodeId == id);
        Nodes.Remove(SelectedNode);
        for (int i = Connections.Count - 1; i >= 0; i--) if (Connections[i].Model.FromNodeId == id || Connections[i].Model.ToNodeId == id) Connections.RemoveAt(i);
        SelectedNode = null; EndChange();
    }
    [RelayCommand] public void DeleteSelectedConnection()
    {
        if (SelectedConnection is null || ActiveBoard is null) return;
        BeginChange(); ActiveBoard.Connections.Remove(SelectedConnection.Model); Connections.Remove(SelectedConnection); SelectedConnection = null; EndChange();
    }
    [RelayCommand] public void ToggleCollapseSelected() { if (SelectedNode is not null) { SelectedNode.IsCollapsed = !SelectedNode.IsCollapsed; EndChange(); } }

    // ---------- Auswahl ----------
    partial void OnSelectedNodeChanged(BoardNodeVm? v)
    {
        HasSelection = v is not null;
        if (v is not null) { SelectedLinkId = v.Model.LinkId; v.LinkPreviewText = LinkedPreview(v); OnPropertyChanged(nameof(SelectedLinkId)); }
    }
    partial void OnSelectedLinkIdChanged(string v) { if (SelectedNode is not null) { SelectedNode.Model.LinkId = v; SelectedNode.LinkPreviewText = LinkedPreview(SelectedNode); OnPropertyChanged(nameof(SelectedNode)); } }
    [RelayCommand] public void ApplyNodeColor(string? hex) { if (SelectedNode is not null && !string.IsNullOrWhiteSpace(hex)) { SelectedNode.Color = hex; EndChange(); } }
    [RelayCommand] public void ApplyNodeAccent(string? hex) { if (SelectedNode is not null && !string.IsNullOrWhiteSpace(hex)) { SelectedNode.Model.AccentColor = hex; SelectedNode.Accent = hex; EndChange(); } }
    public void SelectOnly(BoardNodeVm? vm)
    {
        foreach (var n in Nodes) n.IsSelected = n == vm;
        SelectedNode = vm;
        if (vm is not null) { SelectedConnection = null; SelectedGroup = null; }
    }
    public void ToggleSelect(BoardNodeVm vm)
    {
        vm.IsSelected = !vm.IsSelected;
        SelectedNode = vm.IsSelected ? vm : Nodes.LastOrDefault(n => n.IsSelected);
    }
    public void SelectConnection(BoardConnectionVm? c) { SelectedConnection = c; if (c is not null) SelectOnly(null); }
    public IEnumerable<BoardNodeVm> SelectedNodes => Nodes.Where(n => n.IsSelected);

    // ---------- Verbindungen ----------
    public bool HandleNodeClickForConnect(BoardNodeVm vm)
    {
        if (!IsConnectMode) return false;
        if (_connectSource is null) { _connectSource = vm; SelectOnly(vm); StatusText = $"Quelle: „{vm.Title}“. Jetzt Ziel-Node anklicken."; }
        else
        {
            if (vm.Id != _connectSource.Id) CreateConnection(_connectSource, vm);
            _connectSource = null; IsConnectMode = false; StatusText = "Verbindung erstellt.";
        }
        return true;
    }
    public void CreateConnection(BoardNodeVm from, BoardNodeVm to)
    {
        if (ActiveBoard is null || from.Id == to.Id) return;
        if (ActiveBoard.Connections.Any(c => c.FromNodeId == from.Id && c.ToNodeId == to.Id)) { StatusText = "Diese Verbindung existiert bereits."; return; }
        BeginChange();
        var conn = new BoardConnection { FromNodeId = from.Id, ToNodeId = to.Id };
        ActiveBoard.Connections.Add(conn);
        var vm = new BoardConnectionVm(conn, FindNode); Connections.Add(vm); SelectedConnection = vm; EndChange();
    }
    public void RefreshGeometry() { foreach (var c in Connections) c.RefreshGeometry(); }
    public void OnNodeMoved() => RefreshGeometry();

    // ---------- Suche / Filter ----------
    [RelayCommand] public void RunSearch()
    {
        SearchResults.Clear(); OnPropertyChanged(nameof(HasSearchResults));
        if (string.IsNullOrWhiteSpace(SearchText)) return;
        var q = SearchText.Trim().ToLowerInvariant();
        foreach (var n in Nodes.Where(n => n.Title.ToLowerInvariant().Contains(q) || n.Text.ToLowerInvariant().Contains(q) || n.TypeLabel.ToLowerInvariant().Contains(q) || n.Category.ToLowerInvariant().Contains(q) || n.Tags.ToLowerInvariant().Contains(q)))
            SearchResults.Add(n);
        OnPropertyChanged(nameof(HasSearchResults));
        StatusText = $"{SearchResults.Count} Treffer für „{SearchText}“.";
    }
    [RelayCommand] public void GoToNodeCommand(BoardNodeVm? vm) { if (vm is not null) GoToNode(vm); }
    public void GoToNode(BoardNodeVm vm)
    {
        SelectOnly(vm);
        PanX = 400 - vm.CenterX * Zoom; PanY = 300 - vm.CenterY * Zoom;
        vm.IsHighlighted = true;
        StatusText = $"Zum Node „{vm.Title}“ gesprungen.";
    }
    partial void OnFilterTypeChanged(string v) => ApplyFilter();
    private void ApplyFilter()
    {
        foreach (var n in Nodes)
        {
            var match = FilterType == "Alle Typen" || n.TypeLabel == FilterType;
            if (n.GroupId is not null) { var g = Groups.FirstOrDefault(x => x.Model.Id == n.GroupId); if (g is not null && g.IsCollapsed) match = false; }
            n.IsVisible = match;
        }
        foreach (var g in Groups) g.IsVisible = !g.IsCollapsed;
    }
    [RelayCommand] public void ClearFilter() { FilterType = "Alle Typen"; SearchText = ""; SearchResults.Clear(); OnPropertyChanged(nameof(HasSearchResults)); ApplyFilter(); }

    // ---------- Gruppen ----------
    [RelayCommand] public void CreateGroupFromSelection()
    {
        if (ActiveBoard is null) return;
        var sel = SelectedNodes.ToList();
        if (sel.Count < 2) { StatusText = "Mindestens 2 Nodes auswählen (Shift+Klick), dann Gruppe erstellen."; return; }
        BeginChange();
        var g = new BoardGroup
        {
            Title = "Neue Gruppe",
            X = sel.Min(n => n.X) - 16, Y = sel.Min(n => n.Y) - 16,
            Width = sel.Max(n => n.X + n.Width) - sel.Min(n => n.X) + 32,
            Height = sel.Max(n => n.Y + n.Height) - sel.Min(n => n.Y) + 32,
        };
        ActiveBoard.Groups.Add(g);
        foreach (var n in sel) n.Model.GroupId = g.Id;
        Groups.Add(new BoardGroupVm(g));
        SelectedGroup = Groups.Last(); EndChange();
    }
    [RelayCommand] public void DeleteSelectedGroup()
    {
        if (SelectedGroup is null || ActiveBoard is null) return;
        BeginChange();
        foreach (var n in ActiveBoard.Nodes.Where(n => n.GroupId == SelectedGroup.Model.Id)) n.GroupId = null;
        ActiveBoard.Groups.Remove(SelectedGroup.Model); Groups.Remove(SelectedGroup); SelectedGroup = null; EndChange();
    }
    [RelayCommand] public void ToggleCollapseGroup() { if (SelectedGroup is not null) { SelectedGroup.IsCollapsed = !SelectedGroup.IsCollapsed; ApplyFilter(); EndChange(); } }

    // ---------- Mehrere Boards ----------
    [RelayCommand] public void NewBoard()
    {
        if (_project is null) return;
        BeginChange();
        var wb = new Whiteboard { Title = $"Storyboard { _project.Whiteboards.Count + 1 }" };
        _project.Whiteboards.Add(wb); Boards.Add(wb); SetActiveBoard(wb); StatusText = "Neues Board angelegt.";
    }
    [RelayCommand] public void DeleteActiveBoard()
    {
        if (_project is null || ActiveBoard is null || _project.Whiteboards.Count <= 1) { StatusText = "Mindestens ein Board muss bleiben."; return; }
        _project.Whiteboards.Remove(ActiveBoard); Boards.Remove(ActiveBoard);
        SetActiveBoard(_project.Whiteboards[0]);
    }
    public void SwitchBoard(Whiteboard wb) => SetActiveBoard(wb);

    // ---------- Verknüpfung mit ALICE-Objekten ----------
    public string LinkedPreview(BoardNodeVm vm)
    {
        if (_project is null) return "—";
        return vm.Model.LinkKind switch
        {
            BoardLinkKind.Character => _project.Characters.FirstOrDefault(c => c.Id.ToString() == vm.Model.LinkId)?.Name ?? "(nicht gefunden)",
            BoardLinkKind.World => _project.WorldEntries.FirstOrDefault(w => w.Id.ToString() == vm.Model.LinkId)?.Title ?? "(nicht gefunden)",
            BoardLinkKind.Manuscript => _project.Nodes.FirstOrDefault(n => n.Id.ToString() == vm.Model.LinkId)?.Title ?? "(nicht gefunden)",
            BoardLinkKind.Timeline => _project.TimelineEvents.FirstOrDefault(t => t.Id.ToString() == vm.Model.LinkId)?.Title ?? "(nicht gefunden)",
            _ => "—",
        };
    }

    // ---------- KI-Integration (nur mit Zustimmung, keine Auto-Änderung) ----------
    public string BuildKiContext(string scope)
    {
        IEnumerable<BoardNode> list = scope switch
        {
            "Gesamtes Whiteboard" => ActiveBoard?.Nodes ?? [],
            "Ausgewählte Gruppe" => ActiveBoard?.Nodes.Where(n => n.GroupId == SelectedGroup?.Model.Id) ?? [],
            _ => SelectedNodes.Select(n => n.Model),
        };
        var nodes = list.ToList();
        if (nodes.Count == 0) return "Keine Nodes im gewählten Kontext.";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Storyboard-Kontext ({nodes.Count} Nodes):");
        foreach (var n in nodes) sb.AppendLine($"- [{BoardNodeTypes.LabelOf(n.NodeType)}] {n.Title}: {n.Text} (Tags: {n.Tags}, Status: {n.Status})");
        if (ActiveBoard is not null)
        {
            var inScope = new HashSet<Guid>(nodes.Select(n => n.Id));
            foreach (var c in ActiveBoard.Connections.Where(c => inScope.Contains(c.FromNodeId) && inScope.Contains(c.ToNodeId)))
                sb.AppendLine($"  Verbindung: {ActiveBoard.Nodes.FirstOrDefault(n => n.Id == c.FromNodeId)?.Title} → {ActiveBoard.Nodes.FirstOrDefault(n => n.Id == c.ToNodeId)?.Title} ({BoardConnectionKinds.LabelOf(c.Kind)}){(string.IsNullOrWhiteSpace(c.Label) ? "" : ": " + c.Label)}");
        }
        return sb.ToString();
    }
    [RelayCommand] public void CopyKiContext()
    {
        var ctx = BuildKiContext(KiScope);
        try { System.Windows.Clipboard.SetText(ctx); StatusText = "KI-Kontext in Zwischenablage kopiert. In DeepAI Code einfügen – nichts wurde verändert."; }
        catch (Exception ex) { StatusText = ex.Message; }
    }
    [RelayCommand] public void NodesFromClipboard()
    {
        if (ActiveBoard is null) return;
        string text;
        try { text = System.Windows.Clipboard.GetText(); } catch (Exception ex) { StatusText = ex.Message; return; }
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) { StatusText = "Zwischenablage ist leer."; return; }
        if (System.Windows.MessageBox.Show($"KI-Vorschlag übernehmen? {lines.Count} Zeilen werden als neue Nodes angelegt (bestehende bleiben unverändert).", "Vorschlag übernehmen", System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes) return;
        BeginChange();
        var (x, y) = NextFreePos();
        foreach (var line in lines) AddNodeAt(BoardNodeType.Idea, x, y, line);
        EndChange();
    }
    [RelayCommand] public void SaveBoard() { if (_project is not null) _project.UpdatedAt = DateTime.UtcNow; StatusText = "Storyboard wird mit dem Projekt gespeichert (Autosave)."; }
}
