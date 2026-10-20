using MudBlazor;

namespace OnMuhasebe.Web.Tema;

public static class UygulamaTemasi
{
    public const string Lacivert = "#1E3A5F";
    public const string Turkuaz = "#14B8A6";

    // Grafiklerde sırayla kullanılan renkler.
    public static IReadOnlyList<string> GrafikRenkleri { get; } =
        ["#14B8A6", "#3B82F6", "#F59E0B", "#8B5CF6", "#EF4444", "#0EA5E9", "#22C55E", "#EC4899"];

    public static MudTheme Tema { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Lacivert,
            Secondary = Turkuaz,
            Tertiary = "#F59E0B",
            Info = "#0EA5E9",
            Success = "#16A34A",
            Warning = "#F59E0B",
            Error = "#DC2626",
            AppbarBackground = Lacivert,
            Background = "#F3F6FB",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#334155",
            DrawerIcon = "#64748B",
            TextPrimary = "#0F172A",
            TextSecondary = "#475569",
            LinesDefault = "#E2E8F0",
            TableLines = "#EEF2F7",
            Divider = "#E2E8F0"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#60A5FA",
            Secondary = "#2DD4BF",
            Tertiary = "#FBBF24",
            Info = "#38BDF8",
            Success = "#4ADE80",
            Warning = "#FBBF24",
            Error = "#F87171",
            AppbarBackground = "#0B1324",
            Background = "#070D1A",
            Surface = "#0F1A2E",
            DrawerBackground = "#0B1324",
            DrawerText = "#CBD5E1",
            DrawerIcon = "#94A3B8",
            TextPrimary = "#E2E8F0",
            TextSecondary = "#94A3B8",
            LinesDefault = "#1E293B",
            TableLines = "#1A2539",
            Divider = "#1E293B",
            ActionDefault = "#94A3B8"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Inter", "Roboto", "Helvetica", "Arial", "sans-serif"] },
            H4 = new H4Typography { FontWeight = "700", LetterSpacing = "-0.02em" },
            H5 = new H5Typography { FontWeight = "700", LetterSpacing = "-0.01em" },
            H6 = new H6Typography { FontWeight = "600" },
            Button = new ButtonTypography { FontWeight = "600", TextTransform = "none" }
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "10px" }
    };
}
