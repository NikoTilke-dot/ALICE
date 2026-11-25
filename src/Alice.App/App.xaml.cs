using System.Windows;
using Alice.App.ViewModels;
using Alice.Core.Services;
using Alice.Core.Settings;
using Alice.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Alice.App;

public partial class App : Application
{
    private IServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var sc = new ServiceCollection();
        sc.AddSingleton<ISettingsStore, SettingsStore>();
        sc.AddSingleton<IProjectRepository, ProjectRepository>();
        sc.AddSingleton<IBackupService, BackupService>();
        sc.AddSingleton<ISearchService, SearchService>();
        sc.AddSingleton<IStatsService, StatsService>();
        sc.AddSingleton<IImportExportService, ImportExportService>();
        sc.AddSingleton<IDeepAiCompanionService, DeepAiCompanionService>();
        sc.AddSingleton<IAiProvider, OpenAiCompatibleProvider>();
        sc.AddSingleton<IAiProvider, LocalFallbackProvider>();
        sc.AddTransient<MainViewModel>();
        sc.AddTransient<NavigatorViewModel>();
        sc.AddTransient<EditorViewModel>();
        sc.AddTransient<InspectorViewModel>();
        sc.AddTransient<CorkboardViewModel>();
        sc.AddTransient<OutlinerViewModel>();
        sc.AddTransient<CharactersViewModel>();
        sc.AddTransient<WorldViewModel>();
        sc.AddTransient<TimelineViewModel>();
        sc.AddTransient<ResearchViewModel>();
        sc.AddTransient<AiPanelViewModel>();
        sc.AddTransient<DashboardViewModel>();
        sc.AddTransient<StatsViewModel>();
        sc.AddTransient<SettingsViewModel>();
        sc.AddTransient<SearchViewModel>();
        sc.AddTransient<StoryboardViewModel>();
        _services = sc.BuildServiceProvider();

        // Settings laden
        var settings = _services.GetRequiredService<ISettingsStore>();
        await settings.LoadAsync();

        var vm = _services.GetRequiredService<MainViewModel>();
        var window = new MainWindow(vm);
        await vm.InitializeAsync();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _services?.GetService<ISettingsStore>()?.SaveAsync().GetAwaiter().GetResult(); } catch { }
        base.OnExit(e);
    }
}
