using Microsoft.UI.Xaml;
using EmulatorPCHub.App.Views;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.UI.Services;

namespace EmulatorPCHub.App;

public partial class App : Application
{
    public static HubServices Hub { get; private set; } = null!;
    public static string[] Arguments { get; set; } = [];

    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            HubLog.Error("Unbehandelter Fehler", e.Exception);
            e.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            HubLog.Error("Unbeobachteter Task-Fehler", e.Exception);
            e.SetObserved();
        };
        // Diagnose: Ausnahmen in DispatcherQueue-Rückrufen beenden WinUI-Apps ohne UnhandledException –
        // mit HUB_TRACE_EXCEPTIONS=1 wird jede Ausnahme (auch abgefangene) ins Log geschrieben.
        if (Environment.GetEnvironmentVariable("HUB_TRACE_EXCEPTIONS") == "1")
            AppDomain.CurrentDomain.FirstChanceException += (_, e) => HubLog.Warn($"[trace] {e.Exception.GetType().Name}: {e.Exception.Message}\n{Environment.StackTrace}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Hub = HubServices.Create();
        _window = new MainWindow();

        var cfg = Hub.Config.Current;
        var console = Arguments.Contains("--console") || (!Arguments.Contains("--desktop") && cfg.Ui.StartupMode == "console");
        _window.Activate();
        // Presenter erst nach dem Aktivieren setzen, sonst überschreibt Windows das Vollbild wieder.
        _window.DispatcherQueue.TryEnqueue(() => _window.WindowService.SetConsoleMode(console));

        // --page <name>: direkt eine Seite öffnen (z. B. für Verknüpfungen)
        var pageIdx = Array.IndexOf(Arguments, "--page");
        var page = pageIdx >= 0 && pageIdx + 1 < Arguments.Length ? Arguments[pageIdx + 1].ToLowerInvariant() : null;
        Type? target = page switch
        {
            "controllers" or "controller" => typeof(ControllersPage),
            "downloads" => typeof(DownloadsPage),
            "settings" => typeof(SettingsPage),
            "mods" => typeof(ModsPage),
            "mkwii" => typeof(MarioKartWiiPage),
            "mk8dx" => typeof(MarioKart8DeluxePage),
            "library" => typeof(LibraryPage),
            "profiles" => typeof(ProfilesPage),
            "mii" => typeof(MiiPage),
            "saves" => typeof(SavesPage),
            "stats" => typeof(StatisticsPage),
            _ => null,
        };
        if (target != null)
        {
            _window.Navigate(typeof(HomePage));
            _window.Navigate(target);
        }
        else if (!cfg.SetupCompleted)
        {
            _window.Navigate(typeof(SetupPage));
        }
        else
        {
            _window.Navigate(typeof(HomePage));
        }
        _window.Sounds.Play(Services.UiSound.Startup);

        _ = StartupAsync(_window);
    }

    private static async Task StartupAsync(MainWindow window)
    {
        try
        {
            Hub.ConfigureWheelWizard();
            await Hub.RefreshLibraryAsync();
            if (window.CurrentPage is IHubPage page)
                page.OnShown();

            // --launch <spiel-id>: z. B. für Verknüpfungen/Steam-Export
            var idx = Array.IndexOf(Arguments, "--launch");
            if (idx >= 0 && idx + 1 < Arguments.Length && Hub.Library.Find(Arguments[idx + 1]) is { } game)
                await window.LaunchAsync(game, Hub.Presets.Active(game));
        }
        catch (Exception ex)
        {
            HubLog.Error("Start-Initialisierung fehlgeschlagen", ex);
        }
    }

    public static void OnRedirectedActivation()
    {
        (Current as App)?._window?.DispatcherQueue.TryEnqueue(() => MainWindow.Current.OnActivatedFromOtherInstance());
    }
}
