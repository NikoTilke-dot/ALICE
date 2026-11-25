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
        if (!IsBoardActive()) return;
        var focusedElement = Keyboard.Focus; if (focusedElement is System.Windows.Controls.TextBox) return; // Textbearbeitung nicht abfangen
        var bvm = (StoryboardViewModel)_vm.CurrentWorkspace!;
        if (e.Key == Key.Delete || e.Key == Key.Back) { bvm.DeleteSelectedCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) { bvm.UndoBoardCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) { bvm.RedoBoardCommand.Execute(null); e.Handled = true; }
    }

    // ---------- Viewport: Pan / Zoom / Rechtsklick ----------
    private void BoardViewport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border viewport || !IsBoardActive()) return;
        var bvm = BoardVmOf(viewport); if (bvm is null) return;
        viewport.Focus();
        // Klick auf freie Fläche (nicht auf Node/Verbindung): Pan mit Linksklick, wenn Hand-Werkzeug; sonst Auswahl aufheben
        if (e.OriginalSource is Border || e.OriginalSource is Grid || e.OriginalSource == viewport)
        {
            if (bvm.IsHandTool || e.MiddleButton == MouseButtonState.Pressed)
            {
                _panning = true; _panStart = e.GetPosition(viewport); _panX0 = bvm.PanX; _panY0 = bvm.PanY;
                viewport.CaptureMouse(); e.Handled = true;
            }
            else if (e.LeftButton == MouseButtonState.Pressed && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                bvm.SelectOnly(null); bvm.SelectConnection(null);
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
        if (_panning) { _panning = false; (sender as Border)?.ReleaseMouseCapture(); }
        _pendingNode = null;
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
        card.CaptureMouse();
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
        _resizeNode = node; _resizeW = node.Width; _resizeH = node.Height;
        _dragStart = e.GetPosition((Border)FindViewport()!);
        handle.CaptureMouse(); e.Handled = true;
    }

    private void BoardPort_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsBoardActive()) return;
        var bvm = BoardVmOf(sender as DependencyObject); if (bvm is null) return;
        if ((sender as FrameworkElement)?.DataContext is not BoardNodeVm node) return;
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
            case "Collapse": bvm.ToggleCollapseSelectedCommand.Execute(null); break;
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
