using System.Windows;
using System.Windows.Controls;
using Alice.Core.Services;

namespace Alice.App;

/// <summary>KI schlägt Unterpanels vor; erst nach Anhaken + Bestätigung werden sie
/// als verbundene Kinder des exakten Eltern-Panels erstellt (keine Auto-Generierung).</summary>
public partial class AiSuggestionsDialog : Window
{
    public List<string> Accepted { get; private set; } = [];

    private readonly IAiProvider? _provider;
    private readonly string _parentContext;

    public AiSuggestionsDialog(IAiProvider? provider, string parentTitle, string parentContext)
    {
        InitializeComponent();
        _provider = provider; _parentContext = parentContext;
        ParentText.Text = parentTitle;
        PromptBox.Text = "Gib 4 Ideen für Unterpanels zu „" + parentTitle + "“ (Titel: Beschreibung, eine pro Zeile)";
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_provider is null) { StatusText.Text = "Kein KI-Anbieter verfügbar."; return; }
        if (string.IsNullOrWhiteSpace(PromptBox.Text)) { StatusText.Text = "Bitte Wunsch eingeben."; return; }
        StatusText.Text = "Generiere Ideen …";
        try
        {
            var sys = "Du schlägst Unter-Titel für eine Mindmap vor. Antworte NUR mit einer Liste, eine Zeile pro Vorschlag: Titel: Kurzbeschreibung. Kein Einleitungs- oder Schlusstext.";
            var reply = await _provider.CompleteAsync(sys, PromptBox.Text.Trim(), _parentContext, CancellationToken.None);
            var items = reply.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(l => l.TrimStart('-', '*', '•', ' ', '\t'))
                .Where(l => l.Length > 1).Take(20).ToList();
            SuggestionsPanel.Children.Clear();
            foreach (var it in items) SuggestionsPanel.Children.Add(new CheckBox { Content = it, IsChecked = true, Margin = new Thickness(0, 3, 0, 0), FontSize = 13 });
            StatusText.Text = items.Count == 0 ? "Keine Vorschläge erhalten." : items.Count + " Vorschläge – anhaken und bestätigen.";
        }
        catch (Exception ex) { StatusText.Text = "Fehler: " + ex.Message; }
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        Accepted = SuggestionsPanel.Children.OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => cb.Content?.ToString() ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (Accepted.Count == 0) { StatusText.Text = "Nichts angehakt."; return; }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
