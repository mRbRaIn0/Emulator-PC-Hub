using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Tests;

[Collection("Language")]
public class LanguageTests : IDisposable
{
    public LanguageTests() => Set("de", "en");

    public void Dispose() => Set("de", "en");

    private static void Set(string primary, string secondary) =>
        HubLanguage.Apply(new UiConfig { PrimaryLanguage = primary, SecondaryLanguage = secondary });

    private static GameEntry Game() => new() { Id = "wiiu:x", Title = "Default", Platform = HubPlatform.WiiU };

    [Fact]
    public void DisplayTitle_prefers_primary_then_secondary_then_default()
    {
        var g = Game();
        g.Languages.DetectedTitles["en"] = "English Title";
        g.Languages.DetectedTitles["fr"] = "Titre";
        Assert.Equal("English Title", g.DisplayTitle); // de fehlt → Zweitsprache en

        g.Languages.DetectedTitles["de"] = "Deutscher Titel";
        Assert.Equal("Deutscher Titel", g.DisplayTitle);

        Set("es", "it");
        Assert.Equal("Default", g.DisplayTitle);
        Set("fr", "en");
        Assert.Equal("Titre", g.DisplayTitle);
    }

    [Fact]
    public void Custom_language_title_beats_detected_and_legacy_custom_title_beats_detected()
    {
        var g = Game();
        g.Languages.DetectedTitles["de"] = "Erkannt";
        g.Languages.Titles["de"] = "Eigener";
        Assert.Equal("Eigener", g.DisplayTitle);

        g.Languages.Titles.Clear();
        g.Title = "Mein Name";
        g.CustomTitle = true;
        Assert.Equal("Mein Name", g.DisplayTitle);
    }

    [Fact]
    public void Available_languages_contain_primary_secondary_and_detected_without_duplicates()
    {
        var g = Game();
        g.Languages.Detected.Add("ja");
        g.Languages.DetectedTitles["en"] = "x";
        g.Languages.Covers["fr"] = "c.png";
        var list = g.Languages.Available();
        Assert.Equal(["de", "en", "ja", "fr"], list);
    }

    [Fact]
    public void Ui_language_follows_primary_if_translated_else_secondary()
    {
        Assert.Equal("de", HubLanguage.Ui);
        Set("en", "de");
        Assert.True(HubLanguage.IsEnglish);
        Set("ja", "en");
        Assert.Equal("en", HubLanguage.Ui);
        Set("ja", "ko");
        Assert.Equal("de", HubLanguage.Ui);
    }

    [Fact]
    public void Loc_translates_exact_template_and_fragments_only_in_english()
    {
        Assert.Equal("Einstellungen", Loc.T("Einstellungen"));
        Set("en", "de");
        Assert.Equal("Settings", Loc.T("Einstellungen"));
        Assert.Equal("Player 2: Player 3", Loc.T("Spieler 2: Spieler 3"));
        Assert.Equal("3 games", Loc.T("3 Spiele"));
        Assert.Equal("Unbekannter Text 42", Loc.T("Unbekannter Text 42"));
    }

    [Fact]
    public void Invalid_language_codes_fall_back_to_defaults()
    {
        Set("xx", "");
        Assert.Equal("de", HubLanguage.Primary);
        Assert.Equal("en", HubLanguage.Secondary);
    }
}
