using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.App.Views;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation;
using EmulatorPCHub.UI.Services;
using EmulatorPCHub.UI.ViewModels;
using Windows.System;

namespace EmulatorPCHub.App;

/// <summary>Hauptfenster: obere Leiste, Seitenrahmen, Fußleiste, Start-Overlay und Controller-Routing.</summary>
public sealed partial class MainWindow : Window
{
    public static new MainWindow Current { get; private set; } = null!;

    public HubServices Hub => App.Hub;
    public WindowService WindowService { get; }
    public SoundService Sounds { get; }
    public ControllerHub Controllers => Hub.Input.Controllers;

    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastTick;
    private double _lastSecond = -1;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _toastTimer;

    public MainWindow()
    {
        Current = this;
        InitializeComponent();
        WindowService = new WindowService(this, Hub.Config);
        Sounds = new SoundService(Hub.Config);
        Dialogs.Hwnd = WindowService.Hwnd;
        Controllers.SwapConfirm = Hub.Config.Current.Ui.SwapConfirmButtons;
        Controllers.Start();

        ApplyTheme();
        Root.Loaded += (_, _) => Dialogs.Root = Root.XamlRoot;
        Root.PreviewKeyDown += Root_KeyDown;
        ContentFrame.Navigated += ContentFrame_Navigated;

        Controllers.Action += RouteNav;
        Controllers.HomeComboHeld += () =>
        {
            if (Hub.Pipeline.IsRunning && Hub.Config.Current.Launch.HomeComboStopsGame)
            {
                HubLog.Info("Home + Minus: laufendes Spiel wird beendet");
                Hub.Pipeline.StopCurrent();
            }
        };
        Controllers.DeviceChanged += (device, connected) => DispatcherQueue.TryEnqueue(() =>
        {
            if (Hub.Pipeline.IsRunning)
                return;
            if (connected)
            {
                Sounds.Play(UiSound.Connect);
                ShowToast(device.Player > 0
                    ? $"{device.Name} verbunden → Spieler {device.Player}"
                    : $"{device.Name} verbunden (kein freier Spieler-Platz)");
                if (Hub.Config.Current.Ui.Vibration)
                    Controllers.Rumble(device, 0.3f, 180);
            }
            else
            {
                ShowToast($"{device.Name} getrennt");
            }
            if (CurrentPage is ControllersPage page)
                page.OnShown();
        });
        Controllers.LowBattery += device => DispatcherQueue.TryEnqueue(() =>
            ShowToast($"🔋 Akku schwach: {device.Name} (Spieler {device.Player}) – {device.BatteryPercent} %"));
        Hub.Profiles.Changed += (_, _) => RefreshTopBar();
        WindowService.ModeChanged += console => FullscreenIcon.Glyph = console ? "" : "";

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Closed += (_, _) =>
        {
            _timer.Stop();
            Sounds.Dispose();
        };
        RefreshTopBar();
    }

    public void ApplyTheme()
    {
        Root.RequestedTheme = Hub.Config.Current.Ui.Theme == "switch-light" ? ElementTheme.Light : ElementTheme.Dark;
    }

    /// <summary>Fokussiertes Element – null, solange das Fenster noch keinen XamlRoot hat (früh beim Start).</summary>
    private object? FocusedElement() => Root.XamlRoot is { } root ? FocusManager.GetFocusedElement(root) : null;

    private string? _lastTickError;

    private void Tick()
    {
        // Eine Ausnahme im Timer-Rückruf würde die App sofort beenden (WinUI leitet sie nicht an UnhandledException weiter).
        try
        {
            TickCore();
        }
        catch (Exception ex)
        {
            if (ex.Message != _lastTickError)
                HubLog.Error("Fehler in der Controller-/Oberflächen-Schleife", ex);
            _lastTickError = ex.Message;
        }
    }

    private void TickCore()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = now - _lastTick;
        _lastTick = now;
        var deliver = WindowService.IsForeground && !Hub.Pipeline.IsRunning;
        Controllers.Poll(dt, deliver, Hub.Pipeline.GameProcessRunning);

        if (now - _lastSecond >= 1)
        {
            _lastSecond = now;
            ClockText.Text = EmulatorPCHub.UI.Format.Clock(DateTime.Now, Hub.Config.Current.Ui.Clock24h);
            ControllerCountText.Text = Controllers.ConnectedCount.ToString();
            var low = Controllers.Devices.Where(d => d.BatteryPercent != null).OrderBy(d => d.BatteryPercent).FirstOrDefault();
            ToolTipService.SetToolTip(ControllerCountText, Controllers.Devices.Count == 0 ? "Kein Controller"
                : string.Join(Environment.NewLine, Controllers.Devices.Select(d => $"P{d.Player}: {d.Name} ({d.BatteryText})")));
            ControllerCountText.Foreground = low?.BatteryPercent is <= 15
                ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HubWarnBrush"]
                : ClockText.Foreground;
            NetworkIcon.Glyph = SystemService.IsOnline() ? "" : "";
        }
    }

    public void RefreshTopBar()
    {
        var p = Hub.Profiles.Active;
        ProfileName.Text = p.Name;
        TopAvatar.Spec = p.Avatar;
        TopAvatar.Rebuild();
    }

    // ------------------------------------------------------------------
    // Navigation
    // ------------------------------------------------------------------

    public void Navigate(Type page, object? parameter = null)
    {
        ContentFrame.Navigate(page, parameter);
    }

    public void GoHome()
    {
        if (ContentFrame.Content is HomePage)
            return;
        ContentFrame.Navigate(typeof(HomePage));
        ContentFrame.BackStack.Clear();
    }

    public void GoBack()
    {
        if (ContentFrame.CanGoBack)
        {
            Sounds.Play(UiSound.Back);
            ContentFrame.GoBack();
        }
    }

    private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.Content is IHubPage page)
        {
            HintsText.Text = Loc.T(page.Hints);
            page.OnShown();
        }
        if (e.Content is not HomePage)
            SetBackdrop(null, null, null);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FocusFirstIfNeeded);
    }

    public void SetHints(string hints) => HintsText.Text = Loc.T(hints);

    public object? CurrentPage => ContentFrame.Content;

    /// <summary>Hintergrund passend zum Spiel (Farbe + Cover).</summary>
    public void SetBackdrop(string? accent, string? accentDark, string? image)
    {
        if (accent == null)
        {
            BackdropTint.Fill = null;
            BackdropImage.Source = null;
            return;
        }
        BackdropTint.Fill = GameTileView.Gradient(accent, accentDark ?? accent);
        BackdropImage.Source = image != null && File.Exists(image) ? new BitmapImage(new Uri(image)) : null;
    }

    // ------------------------------------------------------------------
    // Controller- und Tastatur-Routing (Plan Abschnitt 4)
    // ------------------------------------------------------------------

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var focused = FocusedElement();
        var inText = focused is TextBox or PasswordBox or NumberBox;
        switch (e.Key)
        {
            case VirtualKey.F11:
                WindowService.ToggleConsoleMode();
                e.Handled = true;
                return;
            case VirtualKey.Escape:
            case VirtualKey.GoBack:
                if (Dialogs.Current != null || focused is ComboBox { IsDropDownOpen: true })
                    return; // Dialoge/Auswahllisten schließen sich selbst
                RouteNav(NavAction.Back);
                e.Handled = true;
                return;
            case VirtualKey.Back when !inText:
                RouteNav(NavAction.Back);
                e.Handled = true;
                return;
        }

        // Die Startseite hat eine eigene Konsolen-Navigation (Kacheln, runde Buttons).
        if (ContentFrame.Content is HomePage && !inText && Dialogs.Current == null)
        {
            NavAction? action = e.Key switch
            {
                VirtualKey.Left or VirtualKey.A => NavAction.Left,
                VirtualKey.Right or VirtualKey.D => NavAction.Right,
                VirtualKey.Up or VirtualKey.W => NavAction.Up,
                VirtualKey.Down or VirtualKey.S => NavAction.Down,
                VirtualKey.Enter or VirtualKey.Space => NavAction.Accept,
                VirtualKey.F => NavAction.X,
                VirtualKey.P => NavAction.Y,
                VirtualKey.Home => NavAction.Home,
                _ => null,
            };
            if (action != null)
            {
                RouteNav(action.Value);
                e.Handled = true;
            }
        }
    }

    public void RouteNav(NavAction action)
    {
        if (Hub.Pipeline.IsRunning)
            return;

        if (Dialogs.Current != null)
        {
            switch (action)
            {
                case NavAction.Back:
                    Sounds.Play(UiSound.Back);
                    Dialogs.Current.Hide();
                    break;
                case NavAction.Accept:
                    Sounds.Play(UiSound.Select);
                    InvokeFocused();
                    break;
                case NavAction.Up or NavAction.Down or NavAction.Left or NavAction.Right:
                    Sounds.Play(UiSound.Move);
                    FocusManager.TryMoveFocus(ToDirection(action), new FindNextElementOptions { SearchRoot = Dialogs.Current.XamlRoot.Content });
                    break;
            }
            return;
        }

        if (ContentFrame.Content is IHubPage page && page.HandleNav(action))
            return;

        switch (action)
        {
            case NavAction.Up or NavAction.Down or NavAction.Left or NavAction.Right:
                if (MoveFocus(ToDirection(action)))
                    Sounds.Play(UiSound.Move);
                break;
            case NavAction.Accept:
                Sounds.Play(UiSound.Select);
                InvokeFocused();
                break;
            case NavAction.Back:
                GoBack();
                break;
            case NavAction.Home:
                Sounds.Play(UiSound.Back);
                GoHome();
                break;
            case NavAction.Plus:
                Sounds.Play(UiSound.Select);
                Navigate(typeof(SettingsPage));
                break;
        }
    }

    private static FocusNavigationDirection ToDirection(NavAction a) => a switch
    {
        NavAction.Up => FocusNavigationDirection.Up,
        NavAction.Down => FocusNavigationDirection.Down,
        NavAction.Left => FocusNavigationDirection.Left,
        _ => FocusNavigationDirection.Right,
    };

    public bool MoveFocus(FocusNavigationDirection direction)
    {
        var focused = FocusedElement() as DependencyObject;
        if (focused == null || !IsInside(focused, ContentFrame))
            return FocusFirstIfNeeded(force: true);
        return FocusManager.TryMoveFocus(direction, new FindNextElementOptions { SearchRoot = ContentFrame });
    }

    private void FocusFirstIfNeeded() => FocusFirstIfNeeded(false);

    private bool FocusFirstIfNeeded(bool force)
    {
        if (ContentFrame.Content is HomePage)
            return false;
        var focused = FocusedElement() as DependencyObject;
        if (!force && focused != null && IsInside(focused, ContentFrame))
            return false;
        if (FocusManager.FindFirstFocusableElement(ContentFrame) is Control c)
            return c.Focus(FocusState.Keyboard);
        return false;
    }

    private static bool IsInside(DependencyObject element, DependencyObject container)
    {
        for (var d = element; d != null; d = VisualTreeHelper.GetParent(d))
            if (d == container)
                return true;
        return false;
    }

    /// <summary>Löst das fokussierte Element aus (A-Taste).</summary>
    public void InvokeFocused()
    {
        var el = FocusedElement();
        switch (el)
        {
            case ToggleSwitch ts:
                ts.IsOn = !ts.IsOn;
                break;
            case ComboBox cb:
                cb.IsDropDownOpen = !cb.IsDropDownOpen;
                break;
            case ToggleButton tb:
                (FrameworkElementAutomationPeer.CreatePeerForElement(tb) as IToggleProvider)?.Toggle();
                break;
            case ButtonBase b:
                (FrameworkElementAutomationPeer.CreatePeerForElement(b) as IInvokeProvider)?.Invoke();
                break;
            case SelectorItem item:
                var peer = FrameworkElementAutomationPeer.CreatePeerForElement(item);
                (peer?.GetPattern(PatternInterface.SelectionItem) as ISelectionItemProvider)?.Select();
                (peer?.GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();
                break;
        }
    }

    // ------------------------------------------------------------------
    // Spielstart mit Overlay (Plan Abschnitt 18)
    // ------------------------------------------------------------------

    public async Task LaunchAsync(GameEntry game, GamePreset? preset)
    {
        if (Hub.Pipeline.IsRunning)
            return;
        Sounds.Play(UiSound.Launch);
        LaunchHint.Text = Loc.T("Spiel beenden: Home + Minus (Guide + View) 1,5 s halten");
        LaunchTitle.Text = game.DisplayTitle;
        LaunchSubtitle.Text = preset != null && preset.Name != "Standard" ? preset.Name : game.Platform.DisplayName();
        LaunchStep.Text = "";
        LaunchRing.IsActive = true;
        LaunchOverlay.Visibility = Visibility.Visible;
        await Task.Delay(350);

        var progress = new Progress<LaunchProgress>(p => LaunchStep.Text = Loc.T(p.Text));
        var outcome = await Hub.LaunchAsync(game, preset, WindowService, progress);

        LaunchRing.IsActive = false;
        LaunchOverlay.Visibility = Visibility.Collapsed;
        if (!outcome.Success)
        {
            Sounds.Play(UiSound.Error);
            await Dialogs.MessageAsync("Spiel konnte nicht gestartet werden", outcome.Error ?? "Unbekannter Fehler");
        }
        else if (outcome.Duration > TimeSpan.FromSeconds(30))
        {
            ShowToast($"Willkommen zurück! {game.DisplayTitle}: {(int)outcome.Duration.TotalMinutes} Min. gespielt");
        }
        if (ContentFrame.Content is IHubPage page)
            page.OnShown();
    }

    public void ShowToast(string text, double seconds = 3.5)
    {
        ToastText.Text = Loc.T(text);
        Toast.Visibility = Visibility.Visible;
        _toastTimer?.Stop();
        _toastTimer = DispatcherQueue.CreateTimer();
        _toastTimer.Interval = TimeSpan.FromSeconds(seconds);
        _toastTimer.IsRepeating = false;
        _toastTimer.Tick += (_, _) => Toast.Visibility = Visibility.Collapsed;
        _toastTimer.Start();
    }

    // ------------------------------------------------------------------
    // Obere Leiste
    // ------------------------------------------------------------------

    private void ProfileButton_Click(object sender, RoutedEventArgs e) => Navigate(typeof(ProfilesPage));

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => Navigate(typeof(SettingsPage));

    private void FullscreenButton_Click(object sender, RoutedEventArgs e) => WindowService.ToggleConsoleMode();

    public void OnActivatedFromOtherInstance()
    {
        WindowService.AppWindow.Show(true);
        WindowService.BringToFront();
    }
}
