using System.Text.RegularExpressions;

namespace Asahi.WebServices;

/// <summary>
/// Various regex statements.
/// </summary>
public static partial class CompiledRegex
{
    // [GeneratedRegex(@"^https:\/\/v\.animethemes\.moe\/[a-zA-Z0-9\-]+\.webm$")]
    // public static partial Regex AnimeThemesThemeRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9\-]{1,256}\z")]
    public static partial Regex AnimeThemesSlugRegex();
}