using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Emulation;
using WinRT.Interop;

namespace EmulatorPCHub.App.Services;

/// <summary>
/// Fenstersteuerung: Desktop Mode (normales Fenster) und Console Mode (randloses Vollbild, Plan Abschnitt 19),
/// Ausblenden während ein Spiel läuft und Zurückholen in den Vordergrund danach.
/// </summary>
public sealed class WindowService : IHubWindow
{
    private readonly Window _window;
    private readonly ConfigService _config;

    public IntPtr Hwnd { get; }
    public AppWindow AppWindow { get; }
    public bool IsConsoleMode { get; private set; }

    public event Action<bool>? ModeChanged;

    public WindowService(Window window, ConfigService config)
    {
        _window = window;
        _config = config;
        Hwnd = WindowNative.GetWindowHandle(window);
        AppWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Hwnd));
        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "hub.ico");
        if (File.Exists(icon))
            AppWindow.SetIcon(icon);
        AppWindow.Title = "Emulator PC Hub";
    }

    public void SetConsoleMode(bool console)
    {
        IsConsoleMode = console;
        if (console)
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        else
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (AppWindow.Presenter is OverlappedPresenter p)
                p.Maximize();
        }
        ModeChanged?.Invoke(console);
    }

    public void ToggleConsoleMode() => SetConsoleMode(!IsConsoleMode);

    public bool IsForeground => GetForegroundWindow() == Hwnd;

    public void HideForGame()
    {
        if (_config.Current.Launch.HideHubWhilePlaying)
            AppWindow.Hide();
        else if (AppWindow.Presenter is OverlappedPresenter p)
            p.Minimize();
        else
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            (AppWindow.Presenter as OverlappedPresenter)?.Minimize();
        }
    }

    public void RestoreAfterGame()
    {
        _window.DispatcherQueue.TryEnqueue(() =>
        {
            AppWindow.Show(true);
            SetConsoleMode(IsConsoleMode);
            BringToFront();
        });
    }

    /// <summary>Holt den Hub zuverlässig in den Vordergrund (auch gegen die Foreground-Sperre von Windows).</summary>
    public void BringToFront()
    {
        ShowWindow(Hwnd, IsIconic(Hwnd) ? SwRestore : SwShow);
        var fg = GetForegroundWindow();
        var fgThread = GetWindowThreadProcessId(fg, out _);
        var thisThread = GetCurrentThreadId();
        if (fgThread != thisThread)
        {
            AttachThreadInput(fgThread, thisThread, true);
            SetForegroundWindow(Hwnd);
            BringWindowToTop(Hwnd);
            AttachThreadInput(fgThread, thisThread, false);
        }
        else
        {
            SetForegroundWindow(Hwnd);
        }
        _window.Activate();
    }

    private const int SwShow = 5;
    private const int SwRestore = 9;

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
