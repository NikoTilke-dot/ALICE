using System.Collections.ObjectModel;
using System.Windows;
using Alice.Core.Models;
using Alice.Core.Services;
using Alice.Core.Settings;
using Alice.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Alice.App.ViewModels;

/// <summary>KI-Panel: Kontextauswahl, Provider, Konversationen, Schreibtools.</summary>
public partial class AiPanelViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly List<IAiProvider> _providers;
    private AliceProject? _project;
    private CancellationTokenSource? _cts;

    public IAiProvider CurrentProvider => _providers.FirstOrDefault(p => p.Id == _settings.Current.AiProvider) ?? _providers.First();
    public AiConversation? ActiveConversation { get; set; }
    [ObservableProperty] private ObservableCollection<AiConversation> _conversations = [];
    [ObservableProperty] private ObservableCollection<string> _messages = [];
    [ObservableProperty] private string _input = string.Empty;
    [ObservableProperty] private string _contextPreview = string.Empty;
    [ObservableProperty] private string _status = "KI bereit. Kontext wählbar, keine automatische Übernahme.";
    [ObservableProperty] private bool _includeSelection = true;
    [ObservableProperty] private bool _includeScene;
    [ObservableProperty] private bool _includeCharacters;
    [ObservableProperty] private bool _includeWorld;
    [ObservableProperty] private bool _isBusy;
    public ObservableCollection<string> ProviderNames => new(_providers.Select(p => p.DisplayName));
    public IReadOnlyList<DeepAiModel> DeepAiModels => DeepAiCatalog.Models;
    public string SelectedDeepAiModel { get => _settings.Current.DeepAiModel; set { _settings.Current.DeepAiModel = value; OnPropertyChanged(); } }

    public AiPanelViewModel(ISettingsStore settings, IEnumerable<IAiProvider> providers)
    {
        _settings = settings; _providers = providers.ToList();
        _includeSelection = settings.Current.AiIncludeSelection;
        _includeScene = settings.Current.AiIncludeScene;
        _includeCharacters = settings.Current.AiIncludeCharacters;
        _includeWorld = settings.Current.AiIncludeWorld;
    }

    public void Refresh(AliceProject? project)
    {
        _project = project;
        if (project is null) return;
        Conversations = new ObservableCollection<AiConversation>(project.AiConversations);
        if (ActiveConversation is null && Conversations.Count > 0) ActiveConversation = Conversations[0];
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var parts = new List<string>();
        if (_project is null) { ContextPreview = "Kein Projekt."; return; }
        if (_includeSelection) parts.Add($"[Auswahl: {System.Windows.Clipboard.GetText().Length} Zeichen]");
        if (_includeScene) parts.Add("[Szene: ausgewählter Knoten]");
        if (_includeCharacters) parts.Add($"[Figuren: {_project.Characters.Count}]");
        if (_includeWorld) parts.Add($"[Welt: {_project.WorldEntries.Count}]");
        ContextPreview = parts.Count == 0 ? "Kein Kontext gewählt (wird nichts gesendet)." : string.Join(" + ", parts);
    }

    partial void OnIncludeSelectionChanged(bool value) { _settings.Current.AiIncludeSelection = value; UpdatePreview(); }
    partial void OnIncludeSceneChanged(bool value) { _settings.Current.AiIncludeScene = value; UpdatePreview(); }
    partial void OnIncludeCharactersChanged(bool value) { _settings.Current.AiIncludeCharacters = value; UpdatePreview(); }
    partial void OnIncludeWorldChanged(bool value) { _settings.Current.AiIncludeWorld = value; UpdatePreview(); }

    private string BuildContext()
    {
        if (_project is null) return string.Empty;
        var sb = new System.Text.StringBuilder();
        if (_includeSelection) { try { sb.AppendLine("AUSWAHL:\n" + System.Windows.Clipboard.GetText()); } catch { } }
        if (_includeScene && _project.Nodes.Count > 0) sb.AppendLine("\nSZENE (erster Knoten, Demo):\n" + _project.Nodes[0].Content);
        if (_includeCharacters) sb.AppendLine("\nFIGUREN:\n" + string.Join("\n", _project.Characters.Select(c => $"- {c.Name}: {c.Description}")));
        if (_includeWorld) sb.AppendLine("\nWELT:\n" + string.Join("\n", _project.WorldEntries.Select(w => $"- {w.Title}: {w.Description}")));
        return sb.ToString();
    }

    [RelayCommand]
    private async Task Send()
    {
        if (string.IsNullOrWhiteSpace(Input) || IsBusy) return;
        IsBusy = true; Status = "Generiere… (Abbrechen möglich)";
        _cts?.Dispose(); _cts = new CancellationTokenSource();
        var sys = "Du bist Alice Intelligence, Schreibbegleiterin für Romane. Antworte präzise, ohne automatisch zu überschreiben.";
        var ctx = BuildContext();
        var user = Input; Input = string.Empty;
        Messages.Add("Du: " + user);
        try
        {
            var reply = await CurrentProvider.CompleteAsync(sys, user, ctx, _cts.Token);
            Messages.Add("KI: " + reply);
            if (_project is not null)
            {
                ActiveConversation ??= new AiConversation { Title = "Konversation " + DateTime.Now.ToString("HH:mm") };
                ActiveConversation.Messages.Add(new AiMessage { Role = "user", Content = user });
                ActiveConversation.Messages.Add(new AiMessage { Role = "assistant", Content = reply });
                if (!_project.AiConversations.Contains(ActiveConversation)) { _project.AiConversations.Add(ActiveConversation); Conversations.Add(ActiveConversation); }
            }
            Status = "Bereit. Mit 'Einfügen' ins Manuskript übernehmen (Vorschau, kein Auto-Overwrite).";
        }
        catch (OperationCanceledException) { Status = "Abgebrochen."; Messages.Add("(abgebrochen)"); }
        catch (Exception ex) { Status = "Fehler: " + ex.Message; Messages.Add("Fehler: " + ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void InsertToManuscript()
    {
        if (_project is null || Messages.Count == 0) return;
        var last = Messages[^1];
        if (last.StartsWith("KI:")) last = last[3..].Trim();
        // An ersten Knoten anhängen (vorgeschlagen, nicht still überschreiben)
        var target = _project.Nodes.FirstOrDefault(n => !n.IsFolder);
        if (target is not null)
        {
            if (MessageBox.Show($"KI-Text an '{target.Title}' anhängen?\n\n{last[..Math.Min(300, last.Length)]}…", "Ins Manuskript einfügen?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            { target.Content += "\n\n" + last; Status = "Eingefügt."; }
        }
    }

    [RelayCommand]
    private async Task Rewrite() => await Quick("Überarbeite den Text: Klarheit, Fluss, Dialog schärfen. Liefere nur die Überarbeitung.");
    [RelayCommand]
    private async Task ContinueWriting() => await Quick("Führe den Text stilistisch konsistent fort.");
    [RelayCommand]
    private async Task Proofread() => await Quick("Korrekturlesen: Grammatik, Rechtschreibung, Konsistenz. Liste Punkte mit Vorschlägen.");
    [RelayCommand]
    private async Task Summarize() => await Quick("Fasse den Text in 3 Sätzen zusammen.");

    private async Task Quick(string instruction)
    {
        Input = instruction;
        await Send();
    }
}

/// <summary>Einstellungen.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly IDeepAiCompanionService _deepAi;
    public AliceSettings Settings => _settings.Current;
    public IReadOnlyList<DeepAiModel> DeepAiModels => DeepAiCatalog.Models;
    public string SelectedDeepAiModel { get => _settings.Current.DeepAiModel; set { _settings.Current.DeepAiModel = value; OnPropertyChanged(); } }
    [ObservableProperty] private string _status = "Einstellungen werden gespeichert.";
    public SettingsViewModel(ISettingsStore settings, IDeepAiCompanionService deepAi) { _settings = settings; _deepAi = deepAi; }

    [RelayCommand]
    private async Task SaveSettings() { await _settings.SaveAsync(); Status = "Gespeichert."; }

    [RelayCommand]
    private void DetectDeepAi()
    {
        var ok = _deepAi.TryFindDeepAiExe(out var p);
        Status = ok ? $"DeepAI Code gefunden: {p}" : "DeepAI Code nicht gefunden. Pfad in settings.json manuell setzen.";
        var reachable = _deepAi.IsServerReachable(out var d);
        Status += Environment.NewLine + d + (reachable ? " (erreichbar)" : " (nicht erreichbar)");
    }
}

/// <summary>Suche.</summary>
public partial class SearchViewModel : ObservableObject
{
    private readonly ISearchService _search;
    private AliceProject? _project;
    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private string _results = "Suche über Manuskript, Figuren, Welt, Recherche.";
    public SearchViewModel(ISearchService search) => _search = search;
    public void Refresh(AliceProject? p) => _project = p;

    [RelayCommand]
    private void RunSearch()
    {
        if (_project is null) { Results = "Kein Projekt."; return; }
        var r = _search.Search(_project, Query);
        Results = $"Treffer: {r.Total} (Manuskript {r.ManuscriptHits.Count}, Figuren {r.CharacterHits.Count}, Welt {r.WorldHits.Count}, Recherche {r.ResearchHits.Count})";
        if (r.ManuscriptHits.Count > 0) Results += "\n" + string.Join("\n", r.ManuscriptHits.Take(5).Select(n => $"• {n.Title} ({n.WordCount} W)"));
    }
}
