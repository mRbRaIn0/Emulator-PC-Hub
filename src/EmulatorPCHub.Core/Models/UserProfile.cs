using System.Text.Json.Serialization;

namespace EmulatorPCHub.Core.Models;

/// <summary>Lokales Benutzerprofil mit eigenem Avatar (Plan Abschnitt 21).</summary>
public sealed class UserProfile
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonPropertyName("name")] public string Name { get; set; } = "Spieler";
    [JsonPropertyName("avatar")] public AvatarSpec Avatar { get; set; } = new();
    /// <summary>Controller (Geräteschlüssel), der beim Wechsel auf dieses Profil Spieler 1 wird.</summary>
    [JsonPropertyName("preferredController")] public string? PreferredController { get; set; }
    /// <summary>Zugewiesenes Mii aus dem Mii Manager.</summary>
    [JsonPropertyName("miiId")] public string? MiiId { get; set; }
    /// <summary>Cemu-Konto (PersistentId, z. B. 80000001) – trennt Wii-U-Spielstände.</summary>
    [JsonPropertyName("cemuAccount")] public string? CemuAccount { get; set; }
    /// <summary>Eden-Benutzer (UUID, 32 Hex-Zeichen) – trennt Switch-Spielstände.</summary>
    [JsonPropertyName("edenUser")] public string? EdenUser { get; set; }
    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

/// <summary>Eigenes, Mii-ähnliches Avatar-Format (keine Nintendo-Assets).</summary>
public sealed class AvatarSpec
{
    [JsonPropertyName("background")] public string Background { get; set; } = "#FF3FA9F5";
    [JsonPropertyName("skin")] public string Skin { get; set; } = "#FFF5CBA7";
    [JsonPropertyName("hair")] public string Hair { get; set; } = "#FF4A2C1D";
    [JsonPropertyName("hairStyle")] public int HairStyle { get; set; } = 1;
    [JsonPropertyName("eyes")] public int Eyes { get; set; }
    [JsonPropertyName("mouth")] public int Mouth { get; set; }
    [JsonPropertyName("shirt")] public string Shirt { get; set; } = "#FFE60012";

    public static IReadOnlyList<string> Backgrounds { get; } =
        ["#FF3FA9F5", "#FFE60012", "#FF2ECC71", "#FFF39C12", "#FF9B59B6", "#FF1ABC9C", "#FF34495E", "#FFFF6FB5"];
    public static IReadOnlyList<string> Skins { get; } =
        ["#FFFFE0C4", "#FFF5CBA7", "#FFE0AC69", "#FFC68642", "#FF8D5524", "#FF5C3317"];
    public static IReadOnlyList<string> Hairs { get; } =
        ["#FF1B1B1B", "#FF4A2C1D", "#FF8B5A2B", "#FFD9A441", "#FFE8E1C8", "#FFB03A2E", "#FF3D5AFE", "#FFFF77C8"];
    public static int HairStyleCount => 4;
    public static int EyesCount => 3;
    public static int MouthCount => 3;
}
