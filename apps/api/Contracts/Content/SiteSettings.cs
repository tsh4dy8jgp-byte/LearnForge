using System.Text.RegularExpressions;

namespace LearnForge.Api.Contracts.Content;

// Only this allowlisted public section is returned to the browser. Never add deployment secrets here.
public sealed partial record SiteSettings
{
    public string Name { get; init; } = "LearnForge";
    public string Tagline { get; init; } = "A little further, every day.";
    public string Description { get; init; } = "Purposeful practice. Measurable progress.";
    public string? LogoPath { get; init; }
    public string PrimaryColor { get; init; } = "#315b4b";
    public SiteLayout Layout { get; init; } = SiteLayout.Sidebar;
    public SiteColorScheme ColorScheme { get; init; } = SiteColorScheme.Brand;
    public string Font { get; init; } = "system";
    public string Locale { get; init; } = "en-US";
    public string HomePage { get; init; } = "dashboard";
    public string OverviewLabel { get; init; } = "Overview";
    public string LibraryLabel { get; init; } = "Learning library";
    public string HistoryLabel { get; init; } = "Attempts & results";
    public string StudioLabel { get; init; } = "Content studio";

    public bool IsValid()
    {
        static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
        if (!Enum.IsDefined(Layout) || !Enum.IsDefined(ColorScheme)) return false;
        if (!Text(Name, 80) || !Text(Tagline, 160) || !Text(Description, 240) ||
            !new[] { OverviewLabel, LibraryLabel, HistoryLabel, StudioLabel }.All(s => Text(s, 60))) return false;
        if (Locale is not ("en-US" or "en-GB" or "ru-RU" or "kk-KZ") ||
            HomePage is not ("dashboard" or "courses") || Font is not ("system" or "serif")) return false;
        if (LogoPath is not null && (LogoPath.Length > 240 || !LocalAsset().IsMatch(LogoPath))) return false;
        if (PrimaryColor is null || !HexColor().IsMatch(PrimaryColor)) return false;
        // The primary color is text on paper and a button background with white text.
        return Contrast(PrimaryColor, "#ffffff") >= 4.5 && Contrast(PrimaryColor, "#f7f7f2") >= 4.5;
    }

    private static double Contrast(string first, string second)
    {
        static double Luminance(string hex)
        {
            double Channel(int offset)
            {
                var value = Convert.ToInt32(hex.Substring(offset, 2), 16) / 255d;
                return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
            }
            return .2126 * Channel(1) + .7152 * Channel(3) + .0722 * Channel(5);
        }
        var a = Luminance(first); var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
    [GeneratedRegex("^/(?!/)[a-zA-Z0-9/_\\.-]+$")]
    private static partial Regex LocalAsset();
}
