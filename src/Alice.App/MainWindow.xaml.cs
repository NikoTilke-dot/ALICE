using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Media;
using Alice.App.ViewModels;

namespace Alice.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    // Storyboard-Interaktionszustände (nie überlappend; Capture nur während echter Aktivitaet)
    private enum BoardState { Idle, Selecting, MovingNode, MovingCanvas, EditingText, Resizing, CreatingConnection, Marquee }
    private BoardState _boardState = BoardState.Idle;
    private bool _spaceDown;
    private System.Windows.Shapes.Rectangle? _marqueeRect;
    private Point _marqueeStart; private bool _marquee = false;
    // Storyboard-Drag-Zustand
    private bool _panning; private Point _panStart; private double _panX0, _panY0;
    private BoardNodeVm? _dragNode; private Point _dragStart; private List<(BoardNodeVm Node, double X, double Y)> _dragSet = [];
    private BoardNodeVm? _pendingNode; private Point _pendingStart; private List<(BoardNodeVm Node, double X, double Y)> _pendingSet = []; // Drag erst nach 4px Bewegung (kein Versehen bei Klick)
    private BoardNodeVm? _resizeNode; private double _resizeW, _resizeH;
    private BoardNodeVm? _portSource; private Line? _tempLine;
    private readonly HashSet<BoardNodeVm> _highlighted = [];

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        NavigatorTree.SelectedItemChanged += (_, e) =>
        {
            if (e.NewValue is NavigatorNode n) _vm.SelectedNavigatorItem = n;
        };
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewKeyUp += MainWindow_PreviewKeyUp;
    }

    // ---------- Helpers ----------
    private static StoryboardViewModel? BoardVmOf(DependencyObject? obj)
    {
        var cur = obj;
        while (cur is not null)
        {
            if (cur is FrameworkElement fe && fe.DataContext is StoryboardViewModel bvm) return bvm;
            cur = VisualTreeHelper.GetParent(cur);
        }
        return null;
    }
    private static T? FindChild<T>(DependencyObject root, string name) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && fe.Name == name && child is T t) return t;
            var nested = FindChild<T>(child, name);
            if (nested is not null) return nested;
        }
        return null;
    }
    private bool IsBoardActive() => _vm.CurrentWorkspace is StoryboardViewModel;
    private (double x, double y) ToBoard(StoryboardViewModel bvm, Point viewportPos)
        => ((viewportPos.X - bvm.PanX) / bvm.Zoom, (viewportPos.Y - bvm.PanY) / bvm.Zoom);

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) _spaceDown = true;
        if (!IsBoardActive()) return;
        var focusedElement = Keyboard.Focus; if (focusedElement is System.Windows.Controls.TextBox) return; // Textbearbeitung nicht abfangen
        var bvm = (StoryboardViewModel)_vm.CurrentWorkspace!;
        if (e.Key == Key.Delete || e.Key == Key.Back) { bvm.DeleteSelectedCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) { bvm.UndoBoardCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) { bvm.RedoBoardCommand.Execute(null); e.Handled = true; }
    }
    private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.Space) _spaceDown = false; }

    // ---------- Viewport: Pan / Zoom / Rechtsklick ----------
    private void BoardViewport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border viewport || !IsBoardActive()) return;
        var bvm = BoardVmOf(viewport); if (bvm is null) return;
        viewport.Focus();
        // Klick auf freie Fläche (nicht auf Node/Verbindung): Pan mit Linksklick, wenn Hand-Werkzeug; sonst Auswahl aufheben
        if (e.OriginalSource is Border || e.OriginalSource is Grid || e.OriginalSource == viewport)
        {
            if (bvm.IsHandTool || e.MiddleButton == MouseButtonState.Pressed || _spaceDown)
            {
                _boardState = BoardState.MovingCanvas; _panning = true; _panStart = e.GetPosition(viewport); _panX0 = bvm.PanX; _panY0 = bvm.PanY;
                viewport.CaptureMouse(); e.Handled = true;
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Marquee: erst ab >4px Bewegung (einfacher Klick hebt Auswahl auf)
                _boardState = BoardState.Marquee; _marquee = false; _marqueeStart = e.GetPosition(viewport); e.Handled = true;
            }
        }
    }

    private void BoardViewport_PreviewRightDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border viewport || !IsBoardActive()) return;
        var bvm = BoardVmOf(viewport); if (bvm is null) return;
        var (x, y) = ToBoard(bvm, e.GetPosition(viewport));
        bvm.PendingX = x; bvm.PendingY = y;
    }

    private void BoardViewport_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsBoardActive()) return;
        var bvm = BoardVmOf(sender as DependencyObject); if (bvm is null) return;
        if (_boardState == BoardState.Marquee && e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition((Border)FindViewport()!);
            if (!_marquee && Math.Abs(pos.X - _marqueeStart.X) + Math.Abs(pos.Y - _marqueeStart.Y) > 4)
            {
                _marquee = true;
                _marqueeRect = new System.Windows.Shapes.Rectangle { Fill = new SolidColorBrush(Color.FromArgb(60, 124, 124, 244)), Stroke = new SolidColorBrush(Color.FromRgb(124, 124, 244)), StrokeThickness = 1 };
                (FindOverlay() as Canvas)?.Children.Add(_marqueeRect);
            }
            if (_marquee && _marqueeRect is not null)
            {
                Canvas.SetLeft(_marqueeRect, Math.Min(_marqueeStart.X, pos.X)); Canvas.SetTop(_marqueeRect, Math.Min(_marqueeStart.Y, pos.Y));
                _marqueeRect.Width = Math.Abs(pos.X - _marqueeStart.X); _marqueeRect.Height = Math.Abs(pos.Y - _marqueeStart.Y);
            }
            return;
        }
        if (_panning)
        {
            var pos = e.GetPosition((Border)sender);
            bvm.PanX = _panX0 + (pos.X - _panStart.X); bvm.PanY = _panY0 + (pos.Y - _panStart.Y);
            return;
        }
        if (_pendingNode is not null && _dragNode is null)
        {
            var pos0 = e.GetPosition((Border)FindViewport()!);
            if (Math.Abs(pos0.X - _pendingStart.X) + Math.Abs(pos0.Y - _pendingStart.Y) > 4)
            {
                bvm.BeginChange();
                // Echter Drag startet: erst JETZT Capture ergreifen (auf der Viewport).
                _boardState = BoardState.MovingNode;
                if (FindViewport() is FrameworkElement vp) vp.CaptureMouse();
                _dragNode = _pendingNode; _dragSet = _pendingSet; _dragStart = _pendingStart; _pendingNode = null;
            }
            else return;
        }
        if (_dragNode is not null && _dragSet.Count > 0)
        {
            var pos = e.GetPosition((Border)FindViewport()!);
            double dx = (pos.X - _dragStart.X) / bvm.Zoom, dy = (pos.Y - _dragStart.Y) / bvm.Zoom;
            foreach (var (node, ox, oy) in _dragSet) { node.X = ox + dx; node.Y = oy + dy; }
            bvm.OnNodeMoved();
            return;
        }
        if (_resizeNode is not null)
        {
            var pos = e.GetPosition((Border)FindViewport()!);
            double dx = (pos.X - _dragStart.X) / bvm.Zoom, dy = (pos.Y - _dragStart.Y) / bvm.Zoom;
            _resizeNode.Width = Math.Max(140, _resizeW + dx); _resizeNode.Height = Math.Max(70, _resizeH + dy);
            bvm.OnNodeMoved();
            return;
        }
        if (_portSource is not null && _tempLine is not null)
        {
            var (bx, by) = ToBoard(bvm, e.GetPosition((Border)FindViewport()!));
            _tempLine.X2 = bx; _tempLine.Y2 = by;
        }
    }

    private void BoardViewport_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        var bvm = BoardVmOf(sender as DependencyObject);
        // Zentral: Capture IMMER freigeben, egal welches Element sie hat (Panel/Handle/Viewport).
        if (Mouse.Captured is FrameworkElement captured) captured.ReleaseMouseCapture();
        if (_boardState == BoardState.Marquee)
        {
            var up = e.GetPosition((Border)FindViewport()!);
            if (_marquee && bvm is not null)
            {
                var a = ToBoard(bvm, _marqueeStart); var bb = ToBoard(bvm, up);
                bvm.SelectRect(Math.Min(a.x, bb.x), Math.Min(a.y, bb.y), Math.Max(a.x, bb.x), Math.Max(a.y, bb.y));
            }
            else bvm?.SelectOnly(null);
            if (_marqueeRect is not null) { (FindOverlay() as Canvas)?.Children.Remove(_marqueeRect); _marqueeRect = null; }
        }
        _boardState = BoardState.Idle;
        if (_panning) _panning = false;
        _pendingNode = null; _pendingSet = []; _marquee = false;
        if (_dragNode is not null) { _dragNode = null; _dragSet = []; bvm?.EndChange(); }
        if (_resizeNode is not null) { _resizeNode = null; bvm?.EndChange(); }
        if (_portSource is not null)
        {
            // Drop: Ziel-Node unter Cursor suchen
            var target = NodeAt(e.GetPosition((Border)sender));
            if (target is not null && target.Id != _portSource.Id) bvm?.CreateConnection(_portSource, target);
            CleanupTempLine();
            _portSource = null;
        }
    }

    private void BoardViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not Border viewport || !IsBoardActive()) return;
        var bvm = BoardVmOf(viewport); if (bvm is null) return;
        var pos = e.GetPosition(viewport);
        bvm.ZoomAt(e.Delta > 0 ? 1.1 : 0.9, pos.X, pos.Y, viewport.ActualWidth, viewport.ActualHeight);
        e.Handled = true;
    }

    private DependencyObject? FindViewport()
    {
        // Viewport über Visual Tree des ContentControl finden
        var content = FindChild<ContentControl>(this, "");
        // Fallback: suche Border mit Namen BoardViewport im visuellen Baum
        return FindByName(this, "BoardViewport");
    }
    private DependencyObject? FindOverlay() => FindByName(this, "BoardOverlay");
    private static DependencyObject? FindByName(DependencyObject root, string name)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && fe.Name == name) return child;
            var nested = FindByName(child, name);
            if (nested is not null) return nested;
        }
        return null;
    }
    private BoardNodeVm? NodeAt(Point viewportPos)
    {
        if (!IsBoardActive()) return null;
        var bvm = (StoryboardViewModel)_vm.CurrentWorkspace!;
        var (bx, by) = ToBoard(bvm, viewportPos);
        return bvm.Nodes.FirstOrDefault(n => n.IsVisible && bx >= n.X && bx <= n.X + n.Width && by >= n.Y && by <= n.Y + n.Height);
    }
    private void CleanupTempLine()
    {
        var overlay = FindOverlay() as Canvas;
        if (_tempLine is not null && overlay is not null) overlay.Children.Remove(_tempLine);
        _tempLine = null;
    }

    // ---------- Nodes: Auswahl / Drag / Resize / Port ----------
    private void BoardNode_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border card || !IsBoardActive()) return;
        if (e.OriginalSource is TextBox) return; // Textfeld bearbeiten, nicht ziehen
        // Port/Resize-Griffe nicht als Drag behandeln (eigene Handler)
        var src = e.OriginalSource as DependencyObject; var cur = src;
        while (cur is not null) { if (cur is FrameworkElement fe && (fe.Name == "ConnectPort" || fe.Name == "ResizeHandle")) return; cur = VisualTreeHelper.GetParent(cur); }
        var bvm = BoardVmOf(card); if (bvm is null) return;
        if (card.DataContext is not BoardNodeVm node) return;
        if (bvm.HandleNodeClickForConnect(node)) { e.Handled = true; return; }
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) bvm.ToggleSelect(node);
        else if (!node.IsSelected) bvm.SelectOnly(node);
        // Nur bei gedruecktem Linksklick AUF dem Panel + Bewegung > 4px wird gezogen.
        _pendingNode = node;
        _pendingSet = bvm.SelectedNodes.Select(n => (n, n.X, n.Y)).ToList();
        if (_pendingSet.Count == 0) _pendingSet = [(node, node.X, node.Y)];
        _pendingStart = e.GetPosition((Border)FindViewport()!);
        _highlighted.Add(node);
        // KEIN Capture beim Press! Capture erst bei echtem Drag (Schwelle) in PreviewMouseMove.
        // Einfacher Klick darf niemals MouseCapture erhalten -> keine globalen Input-Locks.
        e.Handled = true;
    }

    private void BoardNode_PreviewRightDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border card || !IsBoardActive()) return;
        var bvm = BoardVmOf(card); if (bvm is null) return;
        if (card.DataContext is BoardNodeVm node)
        {
            bvm.SelectOnly(node);
            bvm.PendingX = node.X + node.Width + 60; bvm.PendingY = node.Y;
        }
    }

    private void BoardResize_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border handle || !IsBoardActive()) return;
        var bvm = BoardVmOf(handle); if (bvm is null) return;
        if (handle.DataContext is not BoardNodeVm node) return;
        bvm.BeginChange();
        _boardState = BoardState.Resizing;
        _resizeNode = node; _resizeW = node.Width; _resizeH = node.Height;
        _dragStart = e.GetPosition((Border)FindViewport()!);
        handle.CaptureMouse(); e.Handled = true;
    }

    private void BoardPort_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsBoardActive()) return;
        var bvm = BoardVmOf(sender as DependencyObject); if (bvm is null) return;
        if ((sender as FrameworkElement)?.DataContext is not BoardNodeVm node) return;
        _boardState = BoardState.CreatingConnection;
        _portSource = node;
        var overlay = FindOverlay() as Canvas;
        if (overlay is not null)
        {
            _tempLine = new Line
            {
                X1 = node.Model.RightX, Y1 = node.Model.MidY,
                Stroke = Brushes.Gray, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 6, 4 },
            };
            overlay.Children.Add(_tempLine);
        }
        (sender as FrameworkElement)?.CaptureMouse();
        e.Handled = true;
    }

    // ---------- Inline-KI im Manuskript (Workflow A/B/C/D) ----------
    private void InlineAi_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        { InlineAi_Open(sender, e); e.Handled = true; }
    }
    private void InlineAi_Open_Click(object sender, RoutedEventArgs e) => InlineAi_Open(sender, e);
    private void InlineAi_Open(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not EditorViewModel editor) return;
        if (_vm.Project is null) { _vm.StatusText = "Kein Projekt geöffnet."; return; }
        var p = _vm.Project;
        var selection = tb.SelectionLength > 0 ? tb.SelectedText : string.Empty;
        // Umgebender Text: ±500 Zeichen um den Cursor
        var full = editor.Text ?? string.Empty; var caret = Math.Clamp(tb.SelectionStart, 0, full.Length);
        var from = Math.Max(0, caret - 500); var to = Math.Min(full.Length, caret + 500);
        var scene = full.Substring(from, to - from);
        var chars = string.Join("\n", p.Characters.Select(c => "- " + c.Name + ": " + c.Description));
        var world = string.Join("\n", p.WorldEntries.Select(w => "- " + w.Title + ": " + w.Description));
        var board = _vm.Project.Whiteboards.SelectMany(w => w.Nodes.Select(n => "- [" + Alice.Core.Models.BoardNodeTypes.LabelOf(n.NodeType) + "] " + n.Title + (string.IsNullOrWhiteSpace(n.Text) ? "" : ": " + n.Text))).ToList();
        var dlg = new InlineAiDialog(_vm.Ai.CurrentProvider, selection, scene, chars, world, string.Join("\n", board), p.Title, _vm.Ai.SelectedDeepAiModel) { Owner = this };
        if (dlg.ShowDialog() != true) return; // Verwerfen: Manuskript unverändert
        if (string.IsNullOrEmpty(dlg.GeneratedText)) return;
        // Nur nach ausdrücklicher Bestätigung schreiben - Ersetzen oder Einfügen am Cursor
        if (dlg.ChosenMode == "Replace" && tb.SelectionLength > 0)
        {
            var start = tb.SelectionStart;
            tb.SelectedText = dlg.GeneratedText;
            tb.SelectionStart = start + dlg.GeneratedText.Length; tb.SelectionLength = 0;
        }
        else
        {
            tb.Text = tb.Text.Insert(tb.SelectionStart, dlg.GeneratedText);
            tb.SelectionStart += dlg.GeneratedText.Length; tb.SelectionLength = 0;
        }
        tb.Focus();
        _vm.StatusText = "KI-Text übernommen (Anweisung nicht im Manuskript).";
    }
    private void InlineAi_Marker_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) { var tb2 = FindByName(this, "EditorTextBox") as TextBox; if (tb2 is null) return; tb = tb2; }
        var marker = Alice.Core.Models.InlineAiMarkers.Create("Anweisung hier eingeben");
        tb.Text = tb.Text.Insert(tb.SelectionStart, marker);
        tb.SelectionStart += marker.Length - 24; tb.Focus();
        _vm.StatusText = "{{AI}}-Marker eingefügt – Anweisung editieren, dann „KI-Marker ausführen“.";
    }
    private void InlineAi_RunMarker_Click(object? sender, RoutedEventArgs e)
    {
        var tb = FindByName(this, "EditorTextBox") as TextBox; if (tb is null || _vm.Project is null) return;
        var markers = Alice.Core.Models.InlineAiMarkers.Extract(tb.Text);
        if (markers.Count == 0) { _vm.StatusText = "Kein abgeschlossener {{AI:…}}-Marker gefunden (unabgeschlossene bleiben unangetastet)."; return; }
        var first = markers[0];
        tb.SelectionStart = first.Index; tb.SelectionLength = 0;
        // Marker-Instruction in den Inline-KI-Dialog vorbefüllen und nur nach Bestätigung ersetzen
        var p = _vm.Project;
        var dlg = new InlineAiDialog(_vm.Ai.CurrentProvider, string.Empty, tb.Text, string.Join("\n", p.Characters.Select(c => "- " + c.Name + ": " + c.Description)), string.Join("\n", p.WorldEntries.Select(w => "- " + w.Title + ": " + w.Description)), string.Empty, p.Title, _vm.Ai.SelectedDeepAiModel) { Owner = this };
        dlg.PromptText.Text = first.Instruction;
        if (dlg.ShowDialog() != true) return;
        if (string.IsNullOrEmpty(dlg.GeneratedText)) return;
        var fresh = Alice.Core.Models.InlineAiMarkers.Extract(tb.Text);
        if (fresh.Count == 0) return;
        var m = fresh[0];
        tb.Text = tb.Text.Remove(m.Index, m.Length).Insert(m.Index, dlg.GeneratedText);
        tb.SelectionStart = m.Index + dlg.GeneratedText.Length; tb.Focus();
        _vm.StatusText = "KI-Marker ausgeführt und durch Text ersetzt.";
    }

    // ---------- KI im Storyboard (Vorschlaege / Prompt senden) ----------
    private void OpenAiSuggestions(BoardNodeVm parent)
    {
        var ctx = "Eltern-Panel: " + Alice.Core.Models.BoardNodeTypes.LabelOf(parent.Model.NodeType) + " - " + parent.Title;
        if (!string.IsNullOrWhiteSpace(parent.Text)) ctx += " Inhalt: " + parent.Text;
        var dlg = new AiSuggestionsDialog(_vm.Ai.CurrentProvider, parent.Title, ctx) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var bvm = BoardVmOf(this); if (bvm is null) return;
        foreach (var s in dlg.Accepted)
        {
            var parts = s.Split(':', 2);
            var child = bvm.AddConnectedNode(parent, Alice.Core.Models.BoardNodeType.Idea);
            if (child is null) continue;
            child.Title = parts[0].Trim();
            if (parts.Length > 1) child.Text = parts[1].Trim();
        }
        _vm.StatusText = dlg.Accepted.Count + " KI-Vorschlaege als verbundene Unterpanels erstellt.";
    }
    private void SendPanelToAi(BoardNodeVm node)
    {
        _vm.Ai.Input = (node.Text ?? string.Empty).Trim();
        _vm.Ai.Status = "Panel als Prompt uebernommen - pruefen und Senden druecken.";
    }

    // ---------- Storyboard als PNG (RenderTargetBitmap der Canvas) ----------
    private void BoardExportPng_Click(object sender, RoutedEventArgs e)
    {
        if (!IsBoardActive()) return;
        try
        {
            var world = FindByName(this, "BoardWorld") as FrameworkElement;
            var viewport = FindByName(this, "BoardViewport") as FrameworkElement;
            if (world is null || viewport is null) return;
            // Auf sichtbaren Bereich rendern (Canvas-Transform bleibt erhalten)
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)viewport.ActualWidth, (int)viewport.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(viewport);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "PNG (*.png)|*.png", FileName = "storyboard.png", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
            if (dlg.ShowDialog() != true) return;
            using var fs = File.Create(dlg.FileName); enc.Save(fs);
            if (_vm.CurrentWorkspace is StoryboardViewModel bvm) bvm.StatusText = "Storyboard-PNG exportiert: " + dlg.FileName;
        }
        catch (Exception ex) { if (_vm.CurrentWorkspace is StoryboardViewModel bvm) bvm.StatusText = "PNG-Export-Fehler: " + ex.Message; }
    }

    // ---------- Kontextmenüs (Click-Handler: Command-Bindungen lösen im Popup nicht auf) ----------
    private void BoardCanvasMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || !IsBoardActive()) return;
        var ctx = mi.Parent as ContextMenu; var target = ctx?.PlacementTarget as Border; if (target is null) return;
        var bvm = BoardVmOf(target); if (bvm is null) return;
        var tag = mi.Tag as string ?? "Note";
        switch (tag)
        {
            case "Connect": bvm.ToggleConnectCommand.Execute(null); break;
            case "Paste": bvm.NodesFromClipboardCommand.Execute(null); break;
            default: bvm.AddAtPending(tag); break;
        }
        e.Handled = true;
    }

    private void BoardNodeMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || !IsBoardActive()) return;
        var ctx = mi.Parent as ContextMenu; var target = ctx?.PlacementTarget as Border; if (target is null) return;
        var bvm = BoardVmOf(target); if (bvm is null) return;
        if (target.DataContext is not BoardNodeVm node) return;
        var tag = mi.Tag as string ?? "Note";
        bvm.SelectOnly(node);
        switch (tag)
        {
            case "Connect": bvm.StartConnectFrom(node); break;
            case "Duplicate": bvm.DuplicateSelectedCommand.Execute(null); break;
            case "Copy": bvm.CopySelectedCommand.Execute(null); break;
            case "Cut": bvm.CutSelectedCommand.Execute(null); break;
            case "Paste": bvm.PasteNodeCommand.Execute(null); break;
            case "Collapse": bvm.ToggleCollapseSelectedCommand.Execute(null); break;
            case "Children": bvm.ToggleChildrenCommand.Execute(null); break;
            case "Convert": bvm.ConvertToCharacterCommand.Execute(null); break;
            case "AiIdeas": OpenAiSuggestions(node); break;
            case "AiSend": SendPanelToAi(node); break;
            case "Parent": bvm.GoToParentCommand.Execute(null); break;
            case "Book": bvm.GoToBookCommand.Execute(null); break;
            case "Delete": bvm.DeleteSelectedCommand.Execute(null); break;
            default: bvm.AddConnectedFromMenu(node, tag); break;
        }
        e.Handled = true;
    }

    private void BoardConnection_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsBoardActive()) return;
        var bvm = BoardVmOf(sender as DependencyObject); if (bvm is null) return;
        if ((sender as FrameworkElement)?.DataContext is BoardConnectionVm conn) bvm.SelectConnection(conn);
        e.Handled = true;
    }
}
