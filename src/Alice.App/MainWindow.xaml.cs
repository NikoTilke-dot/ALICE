using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Alice.App.ViewModels;
using Alice.Core.Proofreading;

namespace Alice.App;

public partial class MainWindow : Window
{
    private static Mutex? _singleInstance;
    private readonly MainViewModel _vm;

    // Storyboard-Interaktionszustände (nie überlappend; Capture nur während echter Aktivitaet)
    private enum BoardState { Idle, Selecting, MovingNode, MovingCanvas, EditingText, Resizing, CreatingConnection, Marquee }
    private BoardState _boardState = BoardState.Idle;
    private bool _spaceDown;
    // Editor (DocumentViewer): Seitenzaehler + ausstehende "Next Page"-Navigation
    private System.Windows.Threading.DispatcherTimer? _pageLabelTimer;
    private int _pendingPageNext = -1;
    private bool _fitZoomApplied;
    private readonly GermanSpelling _spell = new GermanSpelling();
    private System.Windows.Threading.DispatcherTimer? _spellTimer;
    private System.Windows.Shapes.Rectangle? _marqueeRect;
    private Point _marqueeStart; private bool _marquee = false;
    // Storyboard-Drag-Zustand
    private bool _panning; private Point _panStart; private double _panX0, _panY0;
    private BoardNodeVm? _dragNode; private Point _dragStart; private List<(BoardNodeVm Node, double X, double Y)> _dragSet = [];
    private FrameworkElement? _dragCard; // Kartenelement des Drags (Capture-Ziel bei Drag-Start)
    private BoardNodeVm? _pendingNode; private Point _pendingStart; private List<(BoardNodeVm Node, double X, double Y)> _pendingSet = []; // Drag erst nach 4px Bewegung (kein Versehen bei Klick)
    private BoardNodeVm? _resizeNode; private double _resizeW, _resizeH;
    private BoardNodeVm? _portSource; private Line? _tempLine;
    private readonly HashSet<BoardNodeVm> _highlighted = [];

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        // Nur EINE ALICE-Instanz (zwei Fenster ueberlagern sich und blockieren Eingabe).
        var m = new Mutex(initiallyOwned: true, "ALICE.SingleInstance", out bool created);
        if (!created) { m.Dispose(); System.Windows.MessageBox.Show("ALICE laeuft bereits."); Application.Current.Shutdown(); return; }
        _singleInstance = m;
        InitializeComponent();
        DataContext = vm;
        // Eingabe-Sicherung: haengendes Capture loesen, wenn keine Board-Geste laeuft.
        Activated += (_, _) => ReleaseStaleCapture();
        PreviewMouseDown += (_, _) => ReleaseStaleCapture();
        NavigatorTree.SelectedItemChanged += (_, e) =>
        {
            if (e.NewValue is NavigatorNode n) _vm.SelectedNavigatorItem = n;
        };
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewKeyUp += MainWindow_PreviewKeyUp;
        // Move/Up AUS FENSTER-Ebene: feuern immer (mit/ohne Capture aus dem Canvas-Tunnel),
        // so dass Drags nicht nach dem ersten Move taub werden.
        PreviewMouseMove += BoardViewport_PreviewMouseMove;
        PreviewMouseUp += BoardViewport_PreviewMouseUp;
        // Editor (Word-Ansicht) an Settings binden und Seitenzaehler nachfuehren.
        _vm.PropertyChanged += (_, e) => { if (e.PropertyName is "CurrentWorkspace" or "ModeIndex") _ = Task.Run(async () => { await Task.Delay(280); await Dispatcher.InvokeAsync(ConnectEditor); }); };
        _pageLabelTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _pageLabelTimer.Tick += (_, _) => RefreshPageLabel();
        // Einmalig verbinden, wenn das Fenster geladen ist (CurrentWorkspace feuert sonst nie nach dem Start).
        Loaded += (_, _) => _ = Task.Run(async () => { await Task.Delay(400); await Dispatcher.InvokeAsync(ConnectEditor); });
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
    // Laeuft kein eigenes Board-Geste (Drag/Pan/Resize/Port), darf keine Captures anhalten.
    private void ReleaseStaleCapture()
    {
        if (_dragNode is null && !_panning && _resizeNode is null && _portSource is null
            && Mouse.Captured is Border captured) // nur Board-Karten/Viewport (keine Popup/Menues)
            captured.ReleaseMouseCapture();
    }
    private (double x, double y) ToBoard(StoryboardViewModel bvm, Point viewportPos)
        => ((viewportPos.X - bvm.PanX) / bvm.Zoom, (viewportPos.Y - bvm.PanY) / bvm.Zoom);

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) _spaceDown = true;
        if (!IsBoardActive()) return;
        // WICHTIG: Keyboard.FocusedElement (mit Property). 'Keyboard.Focus' ohne
        // Argumente ist eine METHODENGRUPPE und nie ein TextBox -> der Guard war
        // wirkungslos und hat Backspace/Delete immer abgefangen (Text-Loeschen tot).
        var focusedElement = Keyboard.FocusedElement; if (focusedElement is System.Windows.Controls.TextBox) return; // Textbearbeitung nicht abfangen
        var bvm = (StoryboardViewModel)_vm.CurrentWorkspace!;
        // KEIN Delete/Backspace auf dem Board: Loeschen nur per Rechtsklick -> Loeschen.
        if (e.Key == Key.Back) { e.Handled = true; return; }
        if (e.Key == Key.Delete) { e.Handled = true; return; }
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
        if (IsOutsideNode(e.OriginalSource as DependencyObject))
        {
            // Canvas bewegen NUR mit Linksklick-Halten auf freier Fläche (getrennt von Panel-Drag).
            // WICHTIG: NodeCard ist ein Border -> alte Pruefung fing ALLE Panel-Klicks als Canvas-Pan ab
            // (Panel-Drag wurde nie gestartet = Canvas statt Panel bewegt).
            // Rechtsklick = kein Drag (Kontextmenü gehört zum Panel). Mittel/Space/Hand bleiben zusaetzlich.
            if (e.LeftButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed || _spaceDown || bvm.IsHandTool)
            {
                _boardState = BoardState.MovingCanvas; _panning = true; _panStart = e.GetPosition(viewport); _panX0 = bvm.PanX; _panY0 = bvm.PanY;
                viewport.CaptureMouse(); e.Handled = true;
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
        var bvm = (StoryboardViewModel)_vm.CurrentWorkspace!; // Fenster-Ebene: sender ist MainWindow
        if (_panning)
        {
            var pos = e.GetPosition((Border)FindViewport()!);
            bvm.PanX = _panX0 + (pos.X - _panStart.X); bvm.PanY = _panY0 + (pos.Y - _panStart.Y);
            return;
        }
        if (_pendingNode is not null && _dragNode is null)
        {
            var pos0 = e.GetPosition((Border)FindViewport()!);
            if (Math.Abs(pos0.X - _pendingStart.X) + Math.Abs(pos0.Y - _pendingStart.Y) > 4)
            {
                bvm.BeginChange();
                // Echter Drag startet: erst JETZT Capture ergreifen - AUF DER KARTE (nicht Viewport).
                _boardState = BoardState.MovingNode;
                _dragNode = _pendingNode; _dragSet = _pendingSet; _dragStart = _pendingStart; _pendingNode = null;
                // Capture NACH der Zustands-Sicherung (verhindert Synth-Event-Doppelstart),
                // Tunneling läuft weiterhin durch die Viewport zum Karten-Handler.
                _dragCard?.CaptureMouse();
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
        var bvm = IsBoardActive() ? (StoryboardViewModel)_vm.CurrentWorkspace! : null;
        // Zentral: NUR Board-Border-Capture freigeben (Karte/Viewport). Niemals ein
        // Button/ComboBox-Capture: das würde dessen Klick verhindern (WPF-Button
        // selbst ergreift beim Druecken Capture - Freigabe davor killt den Click).
        if (Mouse.Captured is Border captured) captured.ReleaseMouseCapture();
        _boardState = BoardState.Idle;
        if (_panning) _panning = false;
        _pendingNode = null; _pendingSet = [];
        if (_dragNode is not null)
        {
            var dragged = _dragNode;
            // Drag & Drop: Panel auf anderes Panel fallen lassen = Kind desselben Panels werden.
            // Nur bei Einzel-Drag (nicht Mehrfach-Auswahl). Gezogenes Panel selbst als Ziel ausschliessen.
            BoardNodeVm? target = null;
            if (_dragSet.Count <= 1 && bvm is not null)
            {
                var pos = e.GetPosition((Border)FindViewport()!);
                var (bx, by) = ToBoard(bvm, pos);
                target = bvm.Nodes.LastOrDefault(n => n.IsVisible && n.Id != dragged.Id && bx >= n.X && bx <= n.X + n.Width && by >= n.Y && by <= n.Y + n.Height);
            }
            _dragNode = null; _dragSet = [];
            if (target is not null && target.Id != dragged.Id)
            {
                bvm?.ReparentChild(dragged, target);
            }
            bvm?.EndChange();
        }
        if (_resizeNode is not null) { _resizeNode = null; bvm?.EndChange(); }
        if (_portSource is not null)
        {
            // Drop: Ziel-Node unter Cursor suchen
            var target = NodeAt(e.GetPosition((Border)FindViewport()!));
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
    /// True wenn der Klick NICHT auf Node/Port/Gruppe landet (freier Canvas).
    private static bool IsOutsideNode(DependencyObject? src)
    {
        var cur = src;
        while (cur is not null)
        {
            if (cur is FrameworkElement fe && (fe.Name == "NodeCard" || fe.Name == "GroupCard" || fe.Name == "ConnectPort")) return false;
            cur = VisualTreeHelper.GetParent(cur);
        }
        return true;
    }
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
        // Editierbares Textfeld (nach Doppelklick) bearbeiten, nicht ziehen. ReadOnly (Standard) = Panel-Koerper, Drag erlaubt.
        if (e.OriginalSource is TextBox editable && !editable.IsReadOnly) return;
        // Port/Resize-Griffe nicht als Drag behandeln (eigene Handler)
        var src = e.OriginalSource as DependencyObject; var cur = src;
        while (cur is not null) { if (cur is FrameworkElement fe && (fe.Name == "ConnectPort" || fe.Name == "ResizeHandle")) return; cur = VisualTreeHelper.GetParent(cur); }
        var bvm = BoardVmOf(card); if (bvm is null) return;
        if (card.DataContext is not BoardNodeVm node) return;
        _boardState = BoardState.Selecting;
        if (bvm.HandleNodeClickForReparent(node)) { _boardState = BoardState.CreatingConnection; e.Handled = true; return; }
        if (bvm.HandleNodeClickForConnect(node)) { _boardState = BoardState.CreatingConnection; e.Handled = true; return; }
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) bvm.ToggleSelect(node);
        else bvm.SelectOnly(node); // normaler Klick = EINZEL-Auswahl (kein Block-Drag); Mehrfach nur mit Shift
        _boardState = BoardState.Selecting;
        // Nur bei gedruecktem Linksklick AUF dem Panel + Bewegung > 4px wird gezogen.
        _dragCard = card;
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

    // ---------- Editor: Word-artige Seitenansicht (DocumentViewer) ----------
    // DataTemplate-Elemente haben KEIN gesetztes Name (nur lokaler Scope) -> per Typ/Tag suchen.
    private FlowDocumentPageViewer? EditorDoc()
    {
        var tagged = FindByTag<FrameworkElement>(this, "EditorDocViewer") as FlowDocumentPageViewer;
        if (tagged is not null) return tagged;
        return FindDescendant<FlowDocumentPageViewer>(this);
    }

    private static T? FindByTag<T>(DependencyObject root, string tag) where T : class
    {
        if (root is FrameworkElement fe && Equals(fe.Tag, tag) && fe is T t) return t;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var hit = FindByTag<T>(VisualTreeHelper.GetChild(root, i), tag);
            if (hit is not null) return hit;
        }
        return null;
    }

    private static T? FindElementByTag<T>(DependencyObject root, string tag) where T : DependencyObject
    {
        if (root is T t && (root is FrameworkElement fe && Equals(fe.Tag, tag))) return t;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var hit = FindElementByTag<T>(VisualTreeHelper.GetChild(root, i), tag);
            if (hit is not null) return hit;
        }
        return null;
    }

    // ---------- Rechtschreibung (lokal, de-DE) ----------
    private void RebuildSpellEngine()
    {
        var st = _vm.SettingsVm.Settings;
        _spell.Enabled = st.SpellCheck;
        _spell.GrammarRules = st.SpellGrammarRules;
        _spell.SetPersonalWords(st.PersonalWords ?? new List<string>());
        var words = new List<string>();
        var proj = _vm.Project;
        if (proj is not null)
        {
            words.AddRange(proj.Characters.Select(c => c.Name));
            words.AddRange(proj.WorldEntries.Select(w => w.Title));
        }
        _spell.SetProjectWords(words);
    }

    private void RefreshSpellDecorations()
    {
        var doc = _vm.Editor.Document;
        if (doc is null) return;
        RebuildSpellEngine();
        if (!_spell.Enabled) return;
        var st = _vm.SettingsVm.Settings;
        var rtb = ActiveRichTextBox();
        Paragraph? caretPara = null;
        try { if (rtb is not null) caretPara = rtb.Selection.Start.Paragraph; } catch { }
        if (st.SpellAutocorrect)
        {
            bool any = false;
            foreach (var p in doc.Blocks.OfType<Paragraph>())
            {
                if (FlowDocumentTools.IsChapterHeaderBlock(p) || ReferenceEquals(p, caretPara)) continue;
                var t = new TextRange(p.ContentStart, p.ContentEnd).Text;
                var fixedText = _spell.AutoCorrect(t);
                if (fixedText != t) { any = true; new TextRange(p.ContentStart, p.ContentEnd).Text = fixedText; }
            }
            if (any) { _vm.Editor.MarkUndoPoint(); _vm.Editor.NotifyDocumentDirty(); }
        }
        FlowDocumentTools.ApplySpellDecorations(doc, _spell, caretPara);
    }

    private void EditorDoc_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var cm = sender as ContextMenu; if (cm is null) return;
        for (int i = cm.Items.Count - 1; i >= 0; i--)
            if (cm.Items[i] is MenuItem mi && mi.Tag is string tg && tg.StartsWith("spell")) cm.Items.RemoveAt(i);
        var rtb = ActiveRichTextBox();
        string word = string.Empty; TextRange? wordRange = null;
        try
        {
            if (rtb is not null)
            {
                var pos = Mouse.GetPosition(rtb);
                var pointer = rtb.GetPositionFromPoint(pos, true);
                if (pointer is not null)
                {
                    var para = pointer.Paragraph;
                    var paraText = new TextRange(para.ContentStart, para.ContentEnd).Text;
                    int offset = new TextRange(para.ContentStart, pointer).Text.Length;
                    var m = GermanSpelling.TokenRegex.Matches(paraText).FirstOrDefault(x => offset >= x.Index && offset <= x.Index + x.Length);
                    if (m is not null)
                    {
                        word = m.Value;
                        var sP = para.ContentStart.GetPositionAtOffset(m.Index);
                        var eP = para.ContentStart.GetPositionAtOffset(m.Index + m.Length);
                        wordRange = new TextRange(sP, eP);
                    }
                }
            }
        }
        catch { }
        if (string.IsNullOrWhiteSpace(word) || !_spell.Enabled) return;
        if (!_spell.IsKnownWord(word))
        {
            var insertAt = 0;
            foreach (var sug in _spell.Suggest(word, 5))
            {
                var item = new MenuItem { Header = "→ " + sug, Tag = "spell-accept" };
                item.Click += (_, _) => { if (wordRange is not null) { wordRange.Text = sug; rtb?.Focus(); } };
                cm.Items.Insert(insertAt++, item);
            }
            var sep = new Separator { Tag = "spell-sep" }; cm.Items.Insert(insertAt++, sep);
        }
        var add = new MenuItem { Header = "\u201E" + word + "\u201C zum Wörterbuch hinzufügen", Tag = "spell-add" };
        add.Click += (_, _) =>
        {
            var st = _vm.SettingsVm.Settings;
            if (!st.PersonalWords.Contains(word)) { st.PersonalWords.Add(word); _ = _vm.SaveSettingsAsync(); }
            _spell.AddPersonalWord(word);
            RefreshSpellDecorations();
        };
        cm.Items.Insert(0, add);
    }

    /// <summary>Aktive RichTextBox des DocumentViewers (fuer Formatierung/Inline-KI). Fokus liegt im Editor auf diesem Element.</summary>
    private RichTextBox? ActiveRichTextBox()
    {
        var fe = Keyboard.FocusedElement as RichTextBox;
        if (fe is not null) return fe;
        var doc = EditorDoc(); if (doc is null) return null;
        return FindDescendant<RichTextBox>(doc, x => x.IsSelectionActive);
    }

    private static T? FindDescendant<T>(DependencyObject root, Func<T, bool>? pred = null) where T : class
    {
        if (root is T t && (pred is null || pred(t))) return t;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var hit = FindDescendant<T>(child, pred);
            if (hit is not null) return hit;
        }
        return null;
    }

    private void HookAllRichTextBoxes(FlowDocumentPageViewer viewer)
    {
        // Jede sichtbare (materialisierte) RichTextBox ans Debounce-Sync haengen
        foreach (var rtb in FindAllDescendants<RichTextBox>(viewer))
        {
            if (rtb.Tag is not string || (string)rtb.Tag != "alice-synced")
            {
                rtb.Tag = "alice-synced";
                rtb.TextChanged += (_, _) => _vm.Editor?.NotifyDocumentDirty();
            }
        }
    }

    private static IEnumerable<T> FindAllDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var deeper in FindAllDescendants<T>(child)) yield return deeper;
        }
    }

    private TextRange? EditorSelectionRange(out bool hadSelection)
    {
        hadSelection = false;
        var rtb = ActiveRichTextBox();
        if (rtb?.Selection is null) return null;
        try
        {
            var sel = rtb.Selection;
            hadSelection = !string.IsNullOrEmpty(sel.Text);
            return new TextRange(sel.Start, sel.End);
        }
        catch { return null; }
    }

    /// <summary>Text an der aktuellen Editor-Position einfuegen oder (bei Auswahl mit Replace) ersetzen.</summary>
    private void EditorInsertText(string text, bool replaceSelection)
    {
        var editor = _vm.Editor;
        var range = EditorSelectionRange(out var hadSelection);
        if (range is null)
        {
            var end = editor.Document.ContentEnd;
            new TextRange(end, end) { Text = text };
            return;
        }
        if (replaceSelection && hadSelection) { range.Text = text; return; }
        new TextRange(range.End, range.End) { Text = text };
    }

    private void ConnectEditor()
    {
        var v = EditorDoc(); var editor = _vm.Editor;
        if (v is null || editor is null) { _pageLabelTimer?.Stop(); _ = Task.Run(async () => { await Task.Delay(900); await Dispatcher.InvokeAsync(ConnectEditor); }); return; }
        var st = _vm.SettingsVm.Settings;
        var doc = editor.Document;
        doc.Background = System.Windows.Media.Brushes.White;
        v.Foreground = System.Windows.Media.Brushes.Black;
        try { doc.Foreground = System.Windows.Media.Brushes.Black; } catch { }
        // WPF misst in DIPs (1/96 Zoll). FRUEHER BUG: Inches gesetzt -> 8.27 DIPs breite Seiten -> 14 Miniseiten.
        const double Dpi = 96.0;
        double marginMm = System.Math.Clamp(st.PageMarginMm, 10, 50);
        doc.PagePadding = new System.Windows.Thickness(marginMm * Dpi / 25.4);
        var (wMm, hMm) = (st.PageSize == "Letter") ? (215.9, 279.4) : (210.0, 297.0);
        if (st.PageOrientation == "Landscape") (wMm, hMm) = (hMm, wMm);
        doc.PageWidth = wMm * Dpi / 25.4;
        doc.PageHeight = hMm * Dpi / 25.4;
        if (st.PageZoom == 1.0 && !_fitZoomApplied && v.ActualWidth > 200)
        {
            st.PageZoom = System.Math.Clamp((v.ActualWidth - 24) / Math.Max(doc.PageWidth, 1), 1.0, 2.2);
            _fitZoomApplied = true;
            _ = _vm.SaveSettingsAsync();
        }
        // FlowDocumentPageViewer.Zoom ist PROZENT (80..200), nicht Faktor!
        v.Zoom = System.Math.Clamp(st.PageZoom * 100, 80, 200);
        // Editor-Sync: TextChanged aller materialisierten RichTextBoxen an das Debounce haengen
        HookAllRichTextBoxes(v);
        if (_vm.Editor.RequestFocusBody)
        {
            _vm.Editor.RequestFocusBody = false;
            EditorFocusBodyStart();
        }
        RefreshPageLabel();
        _pageLabelTimer?.Start();
        _ = _spellTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _spellTimer.Tick += (_, _) => RefreshSpellDecorations();
        RebuildSpellEngine();
        RefreshSpellDecorations();
    }

    /// <summary>Cursor auf den Anfang der ersten Schreibseite setzen (nach "+ Kapitel"/"+ Szene"): Folgeseite nach der Kapitel-Titelseite.</summary>
    private void EditorFocusBodyStart()
    {
        var rtb = ActiveRichTextBox();
        var doc = _vm.Editor.Document;
        if (rtb is null || doc is null) return;
        try
        {
            foreach (var block in doc.Blocks)
            {
                if (block is System.Windows.Documents.Paragraph p && !FlowDocumentTools.IsChapterHeaderBlock(block))
                {
                    var start = p.ContentStart;
                    rtb.Focus();
                    rtb.Selection.Select(start, start);
                    return;
                }
            }
        }
        catch { /* Fokus-Einspruenge darf den Editor nie abbrechen */ }
    }

    private void RefreshPageLabel()
    {
        var v = EditorDoc(); var info = FindElementByTag<System.Windows.Controls.TextBlock>(this, "PageInfoText");
        var zoom = FindElementByTag<System.Windows.Controls.TextBlock>(this, "ZoomLabel");
        if (v is null) return;
        HookAllRichTextBoxes(v); // neu materialisierte Seiten einhaengen
        int cur = Math.Max(v.MasterPageNumber, 1); int total = Math.Max(v.PageCount, 1);
        if (info is not null) info.Text = $"Seite {cur} von {total}";
        if (zoom is not null) zoom.Text = $"{(int)System.Math.Round(v.Zoom)}%";
        if (_pendingPageNext >= 0 && total >= _pendingPageNext)
        {
            v.GoToPage(_pendingPageNext); _pendingPageNext = -1;
        }
    }

    private void PageFirst_Click(object sender, RoutedEventArgs e) { EditorDoc()?.FirstPage(); RefreshPageLabel(); }
    private void PagePrev_Click(object sender, RoutedEventArgs e) { EditorDoc()?.PreviousPage(); RefreshPageLabel(); }
    private void PageLast_Click(object sender, RoutedEventArgs e) { EditorDoc()?.LastPage(); RefreshPageLabel(); }
    private void PageNext_Click(object sender, RoutedEventArgs e)
    {
        var v = EditorDoc(); if (v is null) return;
        if (v.MasterPageNumber < v.PageCount) { v.NextPage(); RefreshPageLabel(); return; }
        // Letzte Seite: Leerabsaetze ANHAENGEN, bis die Paginierung eine neue Seite anlegt.
        // (Ein einzelner Leerabsatz reicht NICHT, wenn die Seite noch halb leer ist.)
        var doc = _vm.Editor.Document;
        int target = v.MasterPageNumber + 1;
        int guard = 0;
        while (v.PageCount < target && guard++ < 200)
        {
            FlowDocumentTools.AppendEmptyParagraph(doc);
            v.InvalidateMeasure(); v.UpdateLayout(); // Paginierung sofort neu berechnen
        }
        if (v.PageCount >= target)
        {
            v.GoToPage(target);
            var end = doc.ContentEnd;
            if (ActiveRichTextBox() is { } rtb)
            {
                try { rtb.Selection.Select(end, end); rtb.Focus(); } catch { }
            }
            RefreshPageLabel();
            return;
        }
        _pendingPageNext = target;
        _pageLabelTimer?.Start();
    }
    private void JumpPage_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter) return;
        var v = EditorDoc(); if (v is null || sender is not System.Windows.Controls.TextBox box) return;
        if (int.TryParse(box.Text.Trim(), out var n) && n >= 1 && n <= System.Math.Max(v.PageCount, 1)) v.GoToPage(n);
        RefreshPageLabel(); e.Handled = true;
    }

    // ---------- Editor: Formatierung ----------
    private void ApplyToggle(string name)
    {
        var range = EditorSelectionRange(out _);
        if (range is null) { _vm.StatusText = "Bitte zuerst in den Text klicken."; return; }
        switch (name)
        {
            case "Bold":
            {
                var cur = range.GetPropertyValue(TextElement.FontWeightProperty);
                range.ApplyPropertyValue(TextElement.FontWeightProperty, cur is FontWeight w && w == FontWeights.Bold ? FontWeights.Normal : FontWeights.Bold);
                break;
            }
            case "Italic":
            {
                var cur = range.GetPropertyValue(TextElement.FontStyleProperty);
                range.ApplyPropertyValue(TextElement.FontStyleProperty, cur is FontStyle f && f == FontStyles.Italic ? FontStyles.Normal : FontStyles.Italic);
                break;
            }
            case "Underline":
            {
                var underline = TextDecorations.Underline;
                // Zustand am ersten Run der Auswahl pruefen
                var first = FirstAffectedRun(range);
                var cur = first?.TextDecorations;
                var isUnderlined = cur is not null && cur.Any(d => d.Location == TextDecorationLocation.Underline);
                foreach (var para in ParagraphsInRange(range).ToList())
                    foreach (var run in para.Inlines.OfType<Run>())
                        run.TextDecorations = isUnderlined ? null : underline;
                break;
            }
            case "AlignLeft": case "AlignCenter": case "AlignRight": case "AlignJustify":
            {
                var align = name == "AlignLeft" ? TextAlignment.Left : name == "AlignCenter" ? TextAlignment.Center : name == "AlignRight" ? TextAlignment.Right : TextAlignment.Justify;
                foreach (var para in ParagraphsInRange(range).ToList()) para.TextAlignment = align;
                break;
            }
            case "Bullets": case "Numbering":
            {
                var style = name == "Bullets" ? System.Windows.TextMarkerStyle.Disc : System.Windows.TextMarkerStyle.Decimal;
                var paras = ParagraphsInRange(range).ToList();
                if (paras.Count == 0) break;
                var home = paras[0].SiblingBlocks;
                var nextSibling = paras[paras.Count - 1].NextBlock;
                foreach (var para in paras) para.SiblingBlocks.Remove(para);
                var list = new List { MarkerStyle = style };
                foreach (var para in paras)
                {
                    var li = new ListItem();
                    li.Blocks.Add(para);
                    list.ListItems.Add(li);
                }
                if (nextSibling is not null) home.InsertBefore(nextSibling, list);
                else home.Add(list);
                break;
            }
            case "Heading1":
                range.ApplyPropertyValue(TextElement.FontSizeProperty, 20.0);
                range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold); break;
            case "Heading2":
                range.ApplyPropertyValue(TextElement.FontSizeProperty, 16.0);
                range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold); break;
            case "Normal":
                range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                if (double.TryParse(_vm.Editor.EditorFontSize, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fs) && fs > 0)
                    range.ApplyPropertyValue(TextElement.FontSizeProperty, fs);
                break;
        }
    }

    private static IEnumerable<Paragraph> ParagraphsInRange(TextRange range)
    {
        var start = range.Start.Paragraph;
        var end = range.End.Paragraph;
        if (start is null || end is null) yield break;
        yield return start;
        var cur = start.NextBlock;
        while (cur is not null && cur != end)
        {
            if (cur is Paragraph p) yield return p;
            cur = cur.NextBlock;
        }
        if (cur is Paragraph last && last != start) yield return last;
    }

    private static Run? FirstAffectedRun(TextRange range)
    {
        foreach (var para in ParagraphsInRange(range))
            foreach (var run in para.Inlines.OfType<Run>())
                return run;
        return null;
    }

    private void FmtBold_Click(object sender, RoutedEventArgs e) => ApplyToggle("Bold");
    private void FmtItalic_Click(object sender, RoutedEventArgs e) => ApplyToggle("Italic");
    private void FmtUnderline_Click(object sender, RoutedEventArgs e) => ApplyToggle("Underline");
    private void FmtAlignLeft_Click(object sender, RoutedEventArgs e) => ApplyToggle("AlignLeft");
    private void FmtAlignCenter_Click(object sender, RoutedEventArgs e) => ApplyToggle("AlignCenter");
    private void FmtAlignRight_Click(object sender, RoutedEventArgs e) => ApplyToggle("AlignRight");
    private void FmtAlignJustify_Click(object sender, RoutedEventArgs e) => ApplyToggle("AlignJustify");
    private void FmtBullets_Click(object sender, RoutedEventArgs e) => ApplyToggle("Bullets");
    private void FmtNumbering_Click(object sender, RoutedEventArgs e) => ApplyToggle("Numbering");
    private void FmtHeading1_Click(object sender, RoutedEventArgs e) => ApplyToggle("Heading1");
    private void FmtHeading2_Click(object sender, RoutedEventArgs e) => ApplyToggle("Heading2");
    private void FmtNormal_Click(object sender, RoutedEventArgs e) => ApplyToggle("Normal");

    private void FmtFont_Changed(object sender, SelectionChangedEventArgs e)
    {
        var editor = _vm.Editor; var box = sender as System.Windows.Controls.ComboBox;
        var choice = (box?.SelectedValue as string) ?? editor.EditorFont;
        if (string.IsNullOrWhiteSpace(choice)) return;
        editor.EditorFont = choice;
        var range = EditorSelectionRange(out var sel);
        System.Windows.Media.FontFamily fam;
        try { fam = new System.Windows.Media.FontFamily(choice); } catch { return; }
        if (range is not null) range.ApplyPropertyValue(TextElement.FontFamilyProperty, fam);
        else { editor.Document.FontFamily = fam; }
    }
    private void FmtSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        var editor = _vm.Editor; var box = sender as System.Windows.Controls.ComboBox;
        var choice = (box?.SelectedValue as string) ?? editor.EditorFontSize;
        if (!double.TryParse(choice, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var size) || size <= 0) return;
        editor.EditorFontSize = choice;
        var range = EditorSelectionRange(out var sel);
        if (range is not null) range.ApplyPropertyValue(TextElement.FontSizeProperty, size);
        else { editor.Document.FontSize = size; }
    }

    // ---------- Editor: Seite ----------
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(10);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(-10);
    private void SetZoom(double deltaPercent)
    {
        var v = EditorDoc(); if (v is null) return;
        var st = _vm.SettingsVm.Settings;
        v.Zoom = System.Math.Clamp(v.Zoom + deltaPercent, 80, 200);
        st.PageZoom = v.Zoom / 100;
        _ = _vm.SettingsVm.SaveSettingsCommand; _ = _vm.SaveSettingsAsync();
        RefreshPageLabel();
    }
    private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        var st = _vm.SettingsVm.Settings; var box = sender as System.Windows.Controls.ComboBox;
        var choice = (box?.SelectedValue as string) ?? st.PageSize;
        st.PageSize = (choice == "Letter") ? "Letter" : "A4";
        _ = _vm.SaveSettingsAsync(); ConnectEditor();
    }
    private void PageOrientation_Click(object sender, RoutedEventArgs e)
    {
        var st = _vm.SettingsVm.Settings;
        st.PageOrientation = (st.PageOrientation == "Landscape") ? "Portrait" : "Landscape";
        _ = _vm.SaveSettingsAsync(); ConnectEditor();
    }
    private void InlineAi_Open_Click(object sender, RoutedEventArgs e) => InlineAi_Open(sender, e);
    private void InlineAi_Open_Click_Sub() { }
    private void InlineAi_Open(object? sender, RoutedEventArgs e)
    {
        if (sender is not DocumentViewer dv || dv.DataContext is not EditorViewModel editor) return;
        if (_vm.Project is null) { _vm.StatusText = "Kein Projekt geöffnet."; return; }
        var p = _vm.Project;
        var range = EditorSelectionRange(out var hadSelection);
        var selection = hadSelection && range is not null ? range.Text : string.Empty;
        // Umgebender Text: +-500 Zeichen um den Cursor (Klartext-Ableitung des FlowDocument)
        var full = editor.Text ?? string.Empty;
        int caretPos = range is not null ? PositionInPlainText(editor.Document, range.End) : Math.Min(full.Length, 0);
        var from = Math.Max(0, caretPos - 500); var to = Math.Min(full.Length, caretPos + 500);
        var scene = full.Substring(from, to - from);
        var chars = string.Join(Environment.NewLine, p.Characters.Select(c => "- " + c.Name + ": " + c.Description));
        var world = string.Join(Environment.NewLine, p.WorldEntries.Select(w => "- " + w.Title + ": " + w.Description));
        var board = _vm.Project.Whiteboards.SelectMany(w => w.Nodes.Select(n => "- [" + Alice.Core.Models.BoardNodeTypes.LabelOf(n.NodeType) + "] " + n.Title + (string.IsNullOrWhiteSpace(n.Text) ? "" : ": " + n.Text))).ToList();
        var dlg = new InlineAiDialog(_vm.Ai.CurrentProvider, selection, scene, chars, world, string.Join(Environment.NewLine, board), p.Title, _vm.Ai.SelectedDeepAiModel) { Owner = this };
        if (dlg.ShowDialog() != true) return; // Verwerfen: Manuskript unveraendert
        if (string.IsNullOrEmpty(dlg.GeneratedText)) return;
        // Nur nach ausdruecklicher Bestaetigung schreiben - Ersetzen oder Einfuegen am Cursor
        EditorInsertText(dlg.GeneratedText, replaceSelection: dlg.ChosenMode == "Replace");
        ActiveRichTextBox()?.Focus();
        _vm.StatusText = "KI-Text uebernommen (Anweisung nicht im Manuskript).";
    }

    /// <summary>Position eines Zeigers im abgeleiteten Klartext (Absaetze mit Zeilenumbruch verbunden).</summary>
    private static int PositionInPlainText(System.Windows.Documents.FlowDocument doc, TextPointer pointer)
    {
        int offset = 0;
        foreach (var block in doc.Blocks)
        {
            var r = new TextRange(block.ContentStart, block.ContentEnd);
            if (pointer.CompareTo(r.End) <= 0)
                return offset + Math.Max(0, pointer.GetOffsetToPosition(r.Start));
            offset += r.Text.Length + 2; // Zeilenumbruch zwischen Absaetzen
        }
        return offset;
    }

    private void InlineAi_Marker_Click(object? sender, RoutedEventArgs e)
    {
        var marker = Alice.Core.Models.InlineAiMarkers.Create("Anweisung hier eingeben");
        EditorInsertText(marker, replaceSelection: false);
        _vm.StatusText = "{{AI}}-Marker eingefuegt - Anweisung editieren, dann \"KI-Marker ausfuehren\".";
    }
    private void InlineAi_RunMarker_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm.Project is null) return;
        var editor = _vm.Editor;
        var markers = Alice.Core.Models.InlineAiMarkers.Extract(editor.Text ?? string.Empty);
        if (markers.Count == 0) { _vm.StatusText = "Kein abgeschlossener {{AI:...}}-Marker gefunden (unabgeschlossene bleiben unangetastet)."; return; }
        var first = markers[0];
        // Marker-Instruction in den Inline-KI-Dialog vorbefuellen und nur nach Bestaetigung ersetzen
        var p = _vm.Project;
        var dlg = new InlineAiDialog(_vm.Ai.CurrentProvider, string.Empty, editor.Text, string.Join(Environment.NewLine, p.Characters.Select(c => "- " + c.Name + ": " + c.Description)), string.Join(Environment.NewLine, p.WorldEntries.Select(w => "- " + w.Title + ": " + w.Description)), string.Empty, p.Title, _vm.Ai.SelectedDeepAiModel) { Owner = this };
        dlg.PromptText.Text = first.Instruction;
        if (dlg.ShowDialog() != true) return;
        if (string.IsNullOrEmpty(dlg.GeneratedText)) return;
        var fresh = Alice.Core.Models.InlineAiMarkers.Extract(editor.Text ?? string.Empty);
        if (fresh.Count == 0) return;
        var m = fresh[0];
        // Marker liegt innerhalb EINES Absatzes? Dann formatierungserhaltend ersetzen.
        int acc = 0; System.Windows.Documents.Paragraph? markerPara = null; int localStart = 0;
        foreach (var block in editor.Document.Blocks)
        {
            var r = new TextRange(block.ContentStart, block.ContentEnd);
            var t = r.Text;
            if (acc + t.Length >= m.Index && acc + t.Length <= m.Index + m.Length)
            {
                markerPara = block as Paragraph; localStart = m.Index - acc;
                break;
            }
            acc += t.Length + 2;
        }
        if (markerPara is not null)
        {
            var pr = new TextRange(markerPara.ContentStart, markerPara.ContentEnd);
            var t = pr.Text;
            if (localStart + m.Length <= t.Length)
            {
                var startPtr = pr.Start.GetPositionAtOffset(localStart);
                var endPtr = pr.Start.GetPositionAtOffset(localStart + m.Length);
                var markerRange = new TextRange(startPtr, endPtr);
                markerRange.Text = dlg.GeneratedText;
                _vm.StatusText = "KI-Marker ausgefuehrt und durch Text ersetzt.";
                return;
            }
        }
        // Marker spannd ueber Absaetze: Kompromiss - Klartext-Neuaufbau (Formatierung entfällt hier)
        var newPlain = (editor.Text ?? string.Empty).Remove(m.Index, m.Length).Insert(m.Index, dlg.GeneratedText);
        editor.LoadPlainTextIntoDocument(newPlain);
        _vm.StatusText = "KI-Marker ausgefuehrt und durch Text ersetzt.";
    }

    // ---------- Panel-Text: Doppelklick = bearbeiten, sonst Panel-Koerper (Drag) ----------
    private void NodeText_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox tb) return;
        tb.IsReadOnly = false; tb.Focus(); tb.SelectAll(); e.Handled = true;
    }
    private void NodeText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb) tb.IsReadOnly = true;
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
        // Canvas-Menue ist entfernt; Handler bleibt nur als toter Code (kein Aufruf mehr).
        var pp = mi.Parent; while (pp is MenuItem pmm) pp = pmm.Parent;
        var ctx = pp as ContextMenu; var target = ctx?.PlacementTarget as Border; if (target is null) return;
        var bvm = BoardVmOf(target); if (bvm is null) return;
        var tag = mi.Tag as string ?? "Note";
        switch (tag)
        {
            case "Connect":
                // Untermenue-Eintraege: Parent ist Eltern-MenuItem, nicht ContextMenu - nach oben laufen.
                var p2 = mi.Parent; while (p2 is MenuItem pm2) p2 = pm2.Parent;
                var ctx2 = p2 as ContextMenu; var target2 = ctx2?.PlacementTarget as Border; if (target2 is null) return;
                if (BoardVmOf(target2) is null) return;
                bvm.ToggleConnectCommand.Execute(null); break;
            case "Paste": bvm.NodesFromClipboardCommand.Execute(null); break;
            default: bvm.AddAtPending(tag); break;
        }
        e.Handled = true;
    }

    private void BoardNodeMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || !IsBoardActive()) return;
        // FEHLERFIX: Untermenue-Eintraege haben als Parent das Eltern-MenuItem, nicht das ContextMenu.
        // Ohne diese Schleife ist target null und KEIN Menueeintrag erzeugte je ein Panel.
        var pp = mi.Parent; while (pp is MenuItem pmm) pp = pmm.Parent;
        var ctx = pp as ContextMenu; var target = ctx?.PlacementTarget as Border; if (target is null) return;
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
            case "Reparent": bvm.StartReparentFrom(node); break;
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

