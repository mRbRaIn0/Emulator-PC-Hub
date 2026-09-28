using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Xml.Linq;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.Controllers;

public sealed record PadForgeStatus(bool Installed, bool Running, bool ExternalControlEnabled, bool FirstRunDone, string? Version, string? ExePath);

/// <summary>
/// Optionale Kompatibilitätsschicht über PadForge (github.com/hifihedgehog/PadForge):
/// Remapping und virtuelle Xbox-Controller für Spiele, die einen Controller nicht direkt unterstützen.
/// Der Hub startet PadForge bei Bedarf im Hintergrund und schaltet Profile über dessen lokale
/// Named Pipe „PadForge.Control“ (Befehle <c>activate &lt;Profil&gt;</c>, <c>deactivate</c>, <c>query</c>).
/// </summary>
public sealed class PadForgeService
{
    public const string PipeName = "PadForge.Control";
    private readonly AppPaths _paths;

    public PadForgeService(AppPaths paths)
    {
        _paths = paths;
    }

    public string Folder => _paths.IntegrationDir("padforge");
    public string? ExePath => File.Exists(Path.Combine(Folder, "PadForge.exe")) ? Path.Combine(Folder, "PadForge.exe") : null;
    public string SettingsFile => Path.Combine(Folder, "PadForge.xml");
    private string FirstRunMarker => Path.Combine(Folder, "hub-firstrun.txt");

    public bool IsRunning => Process.GetProcessesByName("PadForge").Length > 0;

    public PadForgeStatus Status()
    {
        var exe = ExePath;
        string? version = null;
        var marker = Path.Combine(Folder, "hub-version.txt");
        if (File.Exists(marker))
            version = File.ReadAllText(marker).Trim();
        return new PadForgeStatus(exe != null, IsRunning, ReadExternalControl(), File.Exists(FirstRunMarker), version, exe);
    }

    private bool ReadExternalControl()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return false;
            var app = XDocument.Load(SettingsFile).Root?.Element("AppSettings");
            return string.Equals(app?.Element("EnableExternalControl")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Stellt in PadForge.xml ein: externe Steuerung an, minimiert/in den Infobereich starten.
    /// Nur wenn PadForge nicht läuft (sonst überschreibt es die Datei beim Beenden).
    /// </summary>
    public bool ConfigureForHub()
    {
        if (IsRunning || ExePath == null)
            return false;
        try
        {
            XDocument doc;
            if (File.Exists(SettingsFile))
            {
                File.Copy(SettingsFile, SettingsFile + ".hub-backup", overwrite: true);
                doc = XDocument.Load(SettingsFile);
            }
            else
            {
                doc = new XDocument(new XElement("PadForgeSettings"));
            }
            var root = doc.Root!;
            var app = root.Element("AppSettings");
            if (app == null)
                root.Add(app = new XElement("AppSettings"));
            void Set(string name, string value)
            {
                var e = app.Element(name);
                if (e == null) app.Add(new XElement(name, value));
                else e.Value = value;
            }
            Set("EnableExternalControl", "true");
            Set("StartMinimized", "true");
            Set("MinimizeToTray", "true");
            Set("CloseToTray", "true");
            doc.Save(SettingsFile);
            HubLog.Info("PadForge für den Hub konfiguriert (externe Steuerung, minimiert starten)");
            return true;
        }
        catch (Exception ex)
        {
            HubLog.Warn("PadForge.xml konnte nicht angepasst werden", ex);
            return false;
        }
    }

    /// <summary>Startet PadForge im Hintergrund (Windows fragt per UAC nach Administratorrechten).</summary>
    public async Task<bool> EnsureRunningAsync(CancellationToken ct = default)
    {
        var exe = ExePath;
        if (exe == null)
            return false;
        if (!IsRunning)
        {
            ConfigureForHub();
            try
            {
                Process.Start(new ProcessStartInfo(exe)
                {
                    WorkingDirectory = Folder,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized,
                });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                HubLog.Warn("PadForge-Start abgelehnt (UAC?)", ex);
                return false;
            }
        }
        // Auf die Pipe warten
        for (int i = 0; i < 40 && !ct.IsCancellationRequested; i++)
        {
            if (await SendAsync("query", ct) != null)
            {
                File.WriteAllText(FirstRunMarker, DateTimeOffset.Now.ToString("o"));
                return true;
            }
            await Task.Delay(500, ct);
        }
        return false;
    }

    /// <summary>Aktiviert ein PadForge-Profil. Antwort z. B. „ok &lt;Profil&gt;“ oder „error unknown-profile“.</summary>
    public Task<string?> ActivateAsync(string profile, CancellationToken ct = default) =>
        SendAsync("activate " + profile, ct);

    public Task<string?> DeactivateAsync(CancellationToken ct = default) => SendAsync("deactivate", ct);

    public async Task<string?> SendAsync(string command, CancellationToken ct = default)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(1500);
            await pipe.ConnectAsync(timeout.Token);
            var bytes = Encoding.UTF8.GetBytes(command + "\n");
            await pipe.WriteAsync(bytes, timeout.Token);
            await pipe.FlushAsync(timeout.Token);
            var sb = new StringBuilder();
            var buffer = new byte[256];
            while (sb.Length < 1024)
            {
                var n = await pipe.ReadAsync(buffer, timeout.Token);
                if (n <= 0)
                    break;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, n));
                if (sb.ToString().Contains('\n'))
                    break;
            }
            var response = sb.ToString().Trim();
            HubLog.Info($"PadForge: {command} → {response}");
            return response;
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>PadForge sichtbar öffnen (Advanced).</summary>
    public void Open()
    {
        var exe = ExePath;
        if (exe != null)
            Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Folder, UseShellExecute = true });
    }
}
