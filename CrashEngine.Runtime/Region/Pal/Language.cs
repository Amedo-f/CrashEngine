using System.Globalization;

namespace CrashEngine.Runtime.Region.Pal;

// Amedo 2026-11-11
public static class Language
{
    public static int GetSystemLanguageIndex_Pal_17CF98() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "ja" => 0,
            "fr" => 2,
            "es" => 3,
            "de" => 4,
            "it" => 5,
            "nl" => 6,
            "pt" => 7,
            _ => 1,
        };

    public static int StartGame_Pal_100AD4(int systemLanguage) => systemLanguage switch
    {
        2 => 1,
        4 => 2,
        3 => 3,
        5 => 4,
        _ => 0,
    };

    public static string SetGameLanguage_Pal_181B18(int index) => PlaySettings.PalLanguages[index];

    public static string DiscLanguage(string? chosen) =>
        PlaySettings.PalLanguages.FirstOrDefault(l => string.Equals(l, chosen, StringComparison.OrdinalIgnoreCase))
        ?? SetGameLanguage_Pal_181B18(StartGame_Pal_100AD4(GetSystemLanguageIndex_Pal_17CF98()));
}
