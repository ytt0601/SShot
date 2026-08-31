using System.Windows;
using SShot.App.Resources.Themes;

namespace SShot.App.Services;

/// <summary>
/// Swaps the active color theme by replacing the single merged ResourceDictionary tagged with
/// the "ThemeMarker" key in Application.Current.Resources.MergedDictionaries. Every themed
/// control binds to the ThemeXxx brush keys via DynamicResource (see AppStyles.xaml and
/// Theme.*.xaml), so calling Apply repaints whatever windows are open immediately - no window
/// reconstruction needed, unlike UiLayoutMode.
/// </summary>
public sealed class ThemeService
{
    public void Apply(string themeId)
    {
        // settings.json is hand-editable by design and ThemeCatalog.Ids can shrink
        // across releases, so an unknown id must not be trusted - resolving it to a nonexistent
        // Theme.{id}.xaml pack URI throws, and this runs before any window/tray icon exists.
        if (!ThemeCatalog.Ids.Contains(themeId))
        {
            themeId = ThemeCatalog.DefaultId;
        }

        var dictionaries = Application.Current.Resources.MergedDictionaries;

        for (int i = dictionaries.Count - 1; i >= 0; i--)
        {
            if (dictionaries[i].Contains("ThemeMarker"))
            {
                dictionaries.RemoveAt(i);
            }
        }

        dictionaries.Add(new ResourceDictionary { Source = ThemeCatalog.ResourceUri(themeId) });
    }
}
