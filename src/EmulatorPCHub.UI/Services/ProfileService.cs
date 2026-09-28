using System.Text.Json;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.UI.Services;

/// <summary>Lokale Benutzerprofile mit eigenem Avatar (Plan Abschnitt 21).</summary>
public sealed class ProfileService
{
    private readonly string _file;
    private readonly ConfigService _config;
    private List<UserProfile> _profiles;

    public ProfileService(AppPaths paths, ConfigService config)
    {
        _file = Path.Combine(paths.Profiles, "profiles.json");
        _config = config;
        _profiles = Load();
        if (_profiles.Count == 0)
        {
            _profiles.Add(new UserProfile { Name = Environment.UserName });
            Save();
        }
    }

    public IReadOnlyList<UserProfile> All => _profiles;

    public UserProfile Active =>
        _profiles.FirstOrDefault(p => p.Id == _config.Current.ActiveProfileId) ?? _profiles[0];

    public event EventHandler? Changed;

    public void SetActive(UserProfile profile)
    {
        _config.Update(c => c.ActiveProfileId = profile.Id);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public UserProfile Add(string name)
    {
        var p = new UserProfile { Name = name };
        _profiles.Add(p);
        Save();
        return p;
    }

    public void Update(UserProfile profile)
    {
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public UserProfile? Find(string? id) => id == null ? null : _profiles.FirstOrDefault(p => p.Id == id);

    public void Remove(UserProfile profile)
    {
        if (_profiles.Count <= 1)
            return;
        _profiles.Remove(profile);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private List<UserProfile> Load()
    {
        try
        {
            return File.Exists(_file)
                ? JsonSerializer.Deserialize<List<UserProfile>>(File.ReadAllText(_file), HubJson.Options) ?? []
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(_profiles, HubJson.Options));
    }
}
