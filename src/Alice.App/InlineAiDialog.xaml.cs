using System.Windows;
using System.Windows.Controls;
using Alice.Core.Models;
using Alice.Core.Services;
using Alice.Core.Settings;

namespace Alice.App;

/// <summary>Inline-KI: Anweisung + Kontext + Modell, Ergebnis als bearbeitbare Vorschau.
/// Übernahme (Einfügen/Ersetzen) nur per Button – nie automatisches Überschreiben.</summary>
public partial class InlineAiDialog : Window
{
    public TextBox PromptText => PromptBox;
    public string GeneratedText { get; private set; } = string.Empty;
    public string ChosenMode { get; private set; } = string.Empty; // "Insert" | "Replace"

    private readonly IAiProvider? _provider;
    private readonly string _selection;
    private readonly string _scene;
    private readonly string _characters;
    private readonly string _world;
    private readonly string _board;
    private readonly string _bookTitle;
    private readonly string _currentModelId;

    public InlineAiDialog(IAiProvider? provider, string selection, string scene, string characters, string world, string board, string bookTitle, string currentModelId)
    {
        InitializeComponent();
        _provider = provider; _selection = selection; _scene = scene; _characters = characters; _world = world; _board = board;
        _bookTitle = bookTitle; _currentModelId = currentModelId;
        foreach (var m in DeepAiCatalog.Models) ModelBox.Items.Add(new ComboBoxItem { Content = m.DisplayText, Tag = m.Id });
        var idx = 0; for (int i = 0; i < ModelBox.Items.Count; i++) if ((ModelBox.Items[i] as ComboBoxItem)?.Tag as string == currentModelId) { idx = i; break; }
        ModelBox.SelectedIndex = idx;
        RefreshContext();
        CtxSelection.Click += (_, _) => RefreshContext();
        CtxScene.Click += (_, _) => RefreshContext();
        CtxChars.Click += (_, _) => RefreshContext();
        CtxWorld.Click += (_, _) => RefreshContext();
        CtxBoard.Click += (_, _) => RefreshContext();
    }

    private void RefreshContext()
    {
        var parts = new List<string>();
        if (CtxSelection.IsChecked == true && !string.IsNullOrWhiteSpace(_selection)) parts.Add("AUSWAHL:\n" + _selection);
        if (CtxScene.IsChecked == true && !string.IsNullOrWhiteSpace(_scene)) parts.Add("UMGEBENDER TEXT:\n" + _scene);
        if (CtxChars.IsChecked == true && !string.IsNullOrWhiteSpace(_characters)) parts.Add("FIGUREN:\n" + _characters);
        if (CtxWorld.IsChecked == true && !string.IsNullOrWhiteSpace(_world)) parts.Add("WELT:\n" + _world);
        if (CtxBoard.IsChecked == true && !string.IsNullOrWhiteSpace(_board)) parts.Add("STORYBOARD:\n" + _board);
        ContextBox.Text = parts.Count == 0 ? "Kein Kontext gewählt (wird nichts gesendet)." : string.Join("\n\n", parts);
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_provider is null) { StatusText.Text = "Kein KI-Anbieter verfügbar (Einstellungen → KI)."; return; }
        if (string.IsNullOrWhiteSpace(PromptBox.Text)) { StatusText.Text = "Bitte Anweisung eingeben."; return; }
        var model = (ModelBox.SelectedItem as ComboBoxItem)?.Tag as string ?? _currentModelId;
        StatusText.Text = "Generiere mit " + model + " …";
        try
        {
            var sys = "Du bist Alice Intelligence, Schreibbegleiterin für den Roman „" + _bookTitle + "“. " +
                      "Antworte NUR mit dem gewünschten Text (keine Erklärung, kein Code, keine Anführungszeichen um das Ergebnis). " +
                      "Halte dich an den übergebenen Kontext und erfinde keine Kanon-Fakten.";
            var user = PromptBox.Text.Trim();
            var reply = await _provider.CompleteAsync(sys, user, ContextBox.Text, CancellationToken.None);
            ResultBox.Text = reply;
            GeneratedText = reply;
            StatusText.Text = "Fertig. Prüfen/bearbeiten, dann Einfügen oder Ersetzen.";
        }
        catch (Exception ex) { StatusText.Text = "Fehler: " + ex.Message; }
    }

    private void Insert_Click(object sender, RoutedEventArgs e) { GeneratedText = ResultBox.Text; if (string.IsNullOrEmpty(GeneratedText)) return; ChosenMode = "Insert"; DialogResult = true; }
    private void Replace_Click(object sender, RoutedEventArgs e) { GeneratedText = ResultBox.Text; if (string.IsNullOrEmpty(GeneratedText)) return; ChosenMode = "Replace"; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
