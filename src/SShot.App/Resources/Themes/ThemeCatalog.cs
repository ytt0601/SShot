namespace SShot.App.Resources.Themes;

/// <summary>
/// Lists the available color theme ids and resolves each to its ResourceDictionary pack URI and
/// localized display name. Adding a theme means adding both a Theme.{Id}.xaml file and an entry
/// here (and a Strings.Theme{Id} pair in Strings.resx/Strings.ja.resx).
/// </summary>
public static class ThemeCatalog
{
    public static readonly IReadOnlyList<string> Ids =
    [
        "Azure", "Emerald", "Sunset", "Grape", "Amber",
        "Crimson", "Ocean", "Rose", "SlateDark", "Midnight",
    ];

    public const string DefaultId = "Azure";

    public static Uri ResourceUri(string id) =>
        new($"pack://application:,,,/Resources/Themes/Theme.{id}.xaml");

    public static string DisplayName(string id) => id switch
    {
        "Azure" => Strings.ThemeAzure,
        "Emerald" => Strings.ThemeEmerald,
        "Sunset" => Strings.ThemeSunset,
        "Grape" => Strings.ThemeGrape,
        "Amber" => Strings.ThemeAmber,
        "Crimson" => Strings.ThemeCrimson,
        "Ocean" => Strings.ThemeOcean,
        "Rose" => Strings.ThemeRose,
        "SlateDark" => Strings.ThemeSlateDark,
        "Midnight" => Strings.ThemeMidnight,
        _ => id,
    };
}
