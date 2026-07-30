using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ClinicSystem.Data.Repositories;

namespace ClinicSystem.UI.Services;

/// <summary>
/// Holds all color tokens for a single theme — sidebar, primary accent, AND content area.
/// </summary>
public class ThemeDefinition
{
    // ── Identity ────────────────────────────────────────────────────────────
    public string Name    { get; set; } = string.Empty;
    public bool   IsDark  { get; set; }
    public bool   IsCustom { get; set; }

    // ── Sidebar ─────────────────────────────────────────────────────────────
    public string SidebarBackground       { get; set; } = string.Empty;
    public string SidebarForeground       { get; set; } = string.Empty;
    public string SidebarHoverBackground  { get; set; } = string.Empty;
    public string SidebarHoverForeground  { get; set; } = string.Empty;
    public string SidebarActiveBackground { get; set; } = string.Empty;
    public string SidebarActiveForeground { get; set; } = string.Empty;

    // ── Primary Accent ──────────────────────────────────────────────────────
    public string Primary        { get; set; } = string.Empty;
    public string PrimaryHover   { get; set; } = string.Empty;
    public string PrimaryPressed { get; set; } = string.Empty;
    public string PrimaryLight   { get; set; } = string.Empty;

    // ── Content Area (dark/light mode tokens) ───────────────────────────────
    public string ContentBackground { get; set; } = "#F8FAFC";
    public string CardBackground    { get; set; } = "#FFFFFF";
    public string ContentForeground { get; set; } = "#0F172A";
    public string SubtleForeground  { get; set; } = "#64748B";
    public string BorderColor       { get; set; } = "#E2E8F0";
    public string InputBackground   { get; set; } = "#FFFFFF";
}

public static class ThemeService
{
    // ── Built-in theme catalogue ─────────────────────────────────────────────
    public static readonly List<ThemeDefinition> BuiltInThemes = new()
    {
        // ── 1. Slate & Indigo — Light ───────────────────────────────────────
        new ThemeDefinition
        {
            Name                   = "Slate & Indigo",
            IsDark                 = false,
            SidebarBackground      = "#1E293B",
            SidebarForeground      = "#94A3B8",
            SidebarHoverBackground = "#334155",
            SidebarHoverForeground = "#F8FAFC",
            SidebarActiveBackground= "#334155",
            SidebarActiveForeground= "#818CF8",
            Primary                = "#4F46E5",
            PrimaryHover           = "#4338CA",
            PrimaryPressed         = "#3730A3",
            PrimaryLight           = "#EEF2FF",
            ContentBackground      = "#F8FAFC",
            CardBackground         = "#FFFFFF",
            ContentForeground      = "#0F172A",
            SubtleForeground       = "#64748B",
            BorderColor            = "#E2E8F0",
            InputBackground        = "#FFFFFF"
        },

        // ── 2. Slate & Indigo — Dark ────────────────────────────────────────
        new ThemeDefinition
        {
            Name                   = "Slate & Indigo (Dark)",
            IsDark                 = true,
            SidebarBackground      = "#0F172A",
            SidebarForeground      = "#94A3B8",
            SidebarHoverBackground = "#1E293B",
            SidebarHoverForeground = "#F8FAFC",
            SidebarActiveBackground= "#312E81",
            SidebarActiveForeground= "#A5B4FC",
            Primary                = "#6366F1",
            PrimaryHover           = "#4F46E5",
            PrimaryPressed         = "#4338CA",
            PrimaryLight           = "#1E1B4B",
            ContentBackground      = "#0F172A",
            CardBackground         = "#1E293B",
            ContentForeground      = "#F1F5F9",
            SubtleForeground       = "#94A3B8",
            BorderColor            = "#334155",
            InputBackground        = "#1E293B"
        },

        // ── 3. Oceanic Blue — Light (PayFlow-style) ─────────────────────────
        new ThemeDefinition
        {
            Name                   = "Oceanic Blue",
            IsDark                 = false,
            SidebarBackground      = "#0F172A",
            SidebarForeground      = "#7DD3FC",
            SidebarHoverBackground = "#0C4A6E",
            SidebarHoverForeground = "#F0F9FF",
            SidebarActiveBackground= "#0369A1",
            SidebarActiveForeground= "#FFFFFF",
            Primary                = "#0EA5E9",
            PrimaryHover           = "#0284C7",
            PrimaryPressed         = "#0369A1",
            PrimaryLight           = "#E0F2FE",
            ContentBackground      = "#F0F9FF",
            CardBackground         = "#FFFFFF",
            ContentForeground      = "#0C4A6E",
            SubtleForeground       = "#0369A1",
            BorderColor            = "#BAE6FD",
            InputBackground        = "#FFFFFF"
        },

        // ── 4. Oceanic Blue — Dark ───────────────────────────────────────────
        new ThemeDefinition
        {
            Name                   = "Oceanic Blue (Dark)",
            IsDark                 = true,
            SidebarBackground      = "#020617",
            SidebarForeground      = "#38BDF8",
            SidebarHoverBackground = "#0C4A6E",
            SidebarHoverForeground = "#F0F9FF",
            SidebarActiveBackground= "#0369A1",
            SidebarActiveForeground= "#FFFFFF",
            Primary                = "#38BDF8",
            PrimaryHover           = "#0EA5E9",
            PrimaryPressed         = "#0284C7",
            PrimaryLight           = "#082F49",
            ContentBackground      = "#020617",
            CardBackground         = "#0C1A2E",
            ContentForeground      = "#E0F2FE",
            SubtleForeground       = "#7DD3FC",
            BorderColor            = "#164E63",
            InputBackground        = "#0C1A2E"
        },

        // ── 5. Forest Teal — Light (WellNest-style) ──────────────────────────
        new ThemeDefinition
        {
            Name                   = "Forest Teal",
            IsDark                 = false,
            SidebarBackground      = "#134E4A",
            SidebarForeground      = "#99F6E4",
            SidebarHoverBackground = "#0F766E",
            SidebarHoverForeground = "#F0FDFA",
            SidebarActiveBackground= "#0F766E",
            SidebarActiveForeground= "#5EEAD4",
            Primary                = "#0D9488",
            PrimaryHover           = "#0F766E",
            PrimaryPressed         = "#115E59",
            PrimaryLight           = "#CCFBF1",
            ContentBackground      = "#F0FDFA",
            CardBackground         = "#FFFFFF",
            ContentForeground      = "#134E4A",
            SubtleForeground       = "#0F766E",
            BorderColor            = "#99F6E4",
            InputBackground        = "#FFFFFF"
        },

        // ── 6. Forest Teal — Dark ────────────────────────────────────────────
        new ThemeDefinition
        {
            Name                   = "Forest Teal (Dark)",
            IsDark                 = true,
            SidebarBackground      = "#042F2E",
            SidebarForeground      = "#5EEAD4",
            SidebarHoverBackground = "#134E4A",
            SidebarHoverForeground = "#F0FDFA",
            SidebarActiveBackground= "#0D9488",
            SidebarActiveForeground= "#FFFFFF",
            Primary                = "#2DD4BF",
            PrimaryHover           = "#14B8A6",
            PrimaryPressed         = "#0D9488",
            PrimaryLight           = "#042F2E",
            ContentBackground      = "#042F2E",
            CardBackground         = "#0D2321",
            ContentForeground      = "#CCFBF1",
            SubtleForeground       = "#5EEAD4",
            BorderColor            = "#134E4A",
            InputBackground        = "#0D2321"
        },

        // ── 7. Royal Purple — Light ──────────────────────────────────────────
        new ThemeDefinition
        {
            Name                   = "Royal Purple",
            IsDark                 = false,
            SidebarBackground      = "#3B0764",
            SidebarForeground      = "#D8B4FE",
            SidebarHoverBackground = "#581C87",
            SidebarHoverForeground = "#FAF5FF",
            SidebarActiveBackground= "#7E22CE",
            SidebarActiveForeground= "#FFFFFF",
            Primary                = "#9333EA",
            PrimaryHover           = "#7E22CE",
            PrimaryPressed         = "#6B21A8",
            PrimaryLight           = "#F3E8FF",
            ContentBackground      = "#FAF5FF",
            CardBackground         = "#FFFFFF",
            ContentForeground      = "#3B0764",
            SubtleForeground       = "#7E22CE",
            BorderColor            = "#E9D5FF",
            InputBackground        = "#FFFFFF"
        },

        // ── 8. Royal Purple — Dark ───────────────────────────────────────────
        new ThemeDefinition
        {
            Name                   = "Royal Purple (Dark)",
            IsDark                 = true,
            SidebarBackground      = "#1A0036",
            SidebarForeground      = "#C084FC",
            SidebarHoverBackground = "#3B0764",
            SidebarHoverForeground = "#FAF5FF",
            SidebarActiveBackground= "#7E22CE",
            SidebarActiveForeground= "#FFFFFF",
            Primary                = "#A855F7",
            PrimaryHover           = "#9333EA",
            PrimaryPressed         = "#7E22CE",
            PrimaryLight           = "#2E1065",
            ContentBackground      = "#1A0036",
            CardBackground         = "#220047",
            ContentForeground      = "#F3E8FF",
            SubtleForeground       = "#C084FC",
            BorderColor            = "#3B0764",
            InputBackground        = "#220047"
        },

        // ── 9. Minimal Light ─────────────────────────────────────────────────
        new ThemeDefinition
        {
            Name                   = "Minimal Light",
            IsDark                 = false,
            SidebarBackground      = "#FFFFFF",
            SidebarForeground      = "#64748B",
            SidebarHoverBackground = "#F1F5F9",
            SidebarHoverForeground = "#2563EB",
            SidebarActiveBackground= "#EFF6FF",
            SidebarActiveForeground= "#1D4ED8",
            Primary                = "#2563EB",
            PrimaryHover           = "#1D4ED8",
            PrimaryPressed         = "#1E40AF",
            PrimaryLight           = "#DBEAFE",
            ContentBackground      = "#F8FAFC",
            CardBackground         = "#FFFFFF",
            ContentForeground      = "#0F172A",
            SubtleForeground       = "#64748B",
            BorderColor            = "#E2E8F0",
            InputBackground        = "#FFFFFF"
        },

        // ── 10. Midnight Dark ────────────────────────────────────────────────
        new ThemeDefinition
        {
            Name                   = "Midnight Dark",
            IsDark                 = true,
            SidebarBackground      = "#09090B",
            SidebarForeground      = "#A1A1AA",
            SidebarHoverBackground = "#18181B",
            SidebarHoverForeground = "#FAFAFA",
            SidebarActiveBackground= "#27272A",
            SidebarActiveForeground= "#71717A",
            Primary                = "#6366F1",
            PrimaryHover           = "#4F46E5",
            PrimaryPressed         = "#4338CA",
            PrimaryLight           = "#1C1C2B",
            ContentBackground      = "#09090B",
            CardBackground         = "#18181B",
            ContentForeground      = "#FAFAFA",
            SubtleForeground       = "#A1A1AA",
            BorderColor            = "#27272A",
            InputBackground        = "#18181B"
        },

        // ── 11. Warm Amber ───────────────────────────────────────────────────
        new ThemeDefinition
        {
            Name                   = "Warm Amber",
            IsDark                 = false,
            SidebarBackground      = "#1C1917",
            SidebarForeground      = "#FCD34D",
            SidebarHoverBackground = "#292524",
            SidebarHoverForeground = "#FEF9C3",
            SidebarActiveBackground= "#78350F",
            SidebarActiveForeground= "#FDE68A",
            Primary                = "#D97706",
            PrimaryHover           = "#B45309",
            PrimaryPressed         = "#92400E",
            PrimaryLight           = "#FEF3C7",
            ContentBackground      = "#FFFBEB",
            CardBackground         = "#FFFFFF",
            ContentForeground      = "#1C1917",
            SubtleForeground       = "#78350F",
            BorderColor            = "#FDE68A",
            InputBackground        = "#FFFFFF"
        },

        // ── 12. Crimson Red ──────────────────────────────────────────────────
        new ThemeDefinition
        {
            Name                   = "Crimson Red",
            IsDark                 = false,
            SidebarBackground      = "#450A0A",
            SidebarForeground      = "#FCA5A5",
            SidebarHoverBackground = "#7F1D1D",
            SidebarHoverForeground = "#FFF1F2",
            SidebarActiveBackground= "#991B1B",
            SidebarActiveForeground= "#FEF2F2",
            Primary                = "#DC2626",
            PrimaryHover           = "#B91C1C",
            PrimaryPressed         = "#991B1B",
            PrimaryLight           = "#FEE2E2",
            ContentBackground      = "#FFF5F5",
            CardBackground         = "#FFFFFF",
            ContentForeground      = "#450A0A",
            SubtleForeground       = "#991B1B",
            BorderColor            = "#FECACA",
            InputBackground        = "#FFFFFF"
        },
    };

    /// <summary>Runtime list of admin-created custom themes (loaded from DB on startup).</summary>
    public static List<ThemeDefinition> CustomThemes { get; private set; } = new();

    /// <summary>All themes: built-in + custom.</summary>
    public static IEnumerable<ThemeDefinition> AllThemes =>
        BuiltInThemes.Concat(CustomThemes);

    public static string CurrentThemeName { get; private set; } = "Slate & Indigo";

    private static SettingsRepository? _repo;

    // ── Apply ────────────────────────────────────────────────────────────────
    public static void ApplyTheme(string themeName)
    {
        var theme = AllThemes.FirstOrDefault(t => t.Name == themeName);
        if (theme == null) return;

        CurrentThemeName = themeName;

        var res = Application.Current?.Resources;
        if (res == null) return;

        // Sidebar
        SetColorAndBrush(res, "ThemeSidebarBackground",       theme.SidebarBackground);
        SetColorAndBrush(res, "ThemeSidebarForeground",       theme.SidebarForeground);
        SetColorAndBrush(res, "ThemeSidebarHoverBackground",  theme.SidebarHoverBackground);
        SetColorAndBrush(res, "ThemeSidebarHoverForeground",  theme.SidebarHoverForeground);
        SetColorAndBrush(res, "ThemeSidebarActiveBackground", theme.SidebarActiveBackground);
        SetColorAndBrush(res, "ThemeSidebarActiveForeground", theme.SidebarActiveForeground);

        // Primary accent
        SetColorAndBrush(res, "ThemePrimary",        theme.Primary);
        SetColorAndBrush(res, "ThemePrimaryHover",   theme.PrimaryHover);
        SetColorAndBrush(res, "ThemePrimaryPressed", theme.PrimaryPressed);
        SetColorAndBrush(res, "ThemePrimaryLight",   theme.PrimaryLight);

        // Content area (dark / light)
        SetColorAndBrush(res, "ThemeContentBackground", theme.ContentBackground);
        SetColorAndBrush(res, "ThemeCardBackground",    theme.CardBackground);
        SetColorAndBrush(res, "ThemeContentForeground", theme.ContentForeground);
        SetColorAndBrush(res, "ThemeSubtleForeground",  theme.SubtleForeground);
        SetColorAndBrush(res, "ThemeBorder",            theme.BorderColor);
        SetColorAndBrush(res, "ThemeInputBackground",   theme.InputBackground);
    }

    // ── Custom theme CRUD ────────────────────────────────────────────────────
    public static void SaveCustomTheme(ThemeDefinition theme)
    {
        if (_repo == null) return;
        theme.IsCustom = true;

        // Remove old entry with same name if it exists
        CustomThemes.RemoveAll(t => t.Name == theme.Name);
        CustomThemes.Add(theme);

        PersistCustomThemes();
    }

    public static void DeleteCustomTheme(string themeName)
    {
        CustomThemes.RemoveAll(t => t.Name == themeName);
        PersistCustomThemes();
    }

    public static void LoadCustomThemes()
    {
        if (_repo == null) return;
        try
        {
            var dict = _repo.GetAll();
            if (dict.TryGetValue("CustomThemesJson", out var json) && !string.IsNullOrWhiteSpace(json))
            {
                var list = JsonSerializer.Deserialize<List<ThemeDefinition>>(json);
                if (list != null)
                {
                    foreach (var t in list) t.IsCustom = true;
                    CustomThemes = list;
                }
            }
        }
        catch { /* ignore — start fresh */ }
    }

    private static void PersistCustomThemes()
    {
        if (_repo == null) return;
        try
        {
            var json = JsonSerializer.Serialize(CustomThemes);
            _repo.SetValue("CustomThemesJson", json);
        }
        catch { /* ignore */ }
    }

    // ── Initialization ───────────────────────────────────────────────────────
    public static void Initialize(SettingsRepository repo)
    {
        _repo = repo;
        try
        {
            LoadCustomThemes();

            var dict = repo.GetAll();
            if (dict.TryGetValue("AppTheme", out var savedTheme) &&
                AllThemes.Any(t => t.Name == savedTheme))
            {
                ApplyTheme(savedTheme);
                return;
            }
        }
        catch { /* fall through to default */ }

        ApplyTheme("Slate & Indigo");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static void SetColorAndBrush(IResourceDictionary resources, string keyPrefix, string hexColor)
    {
        if (Color.TryParse(hexColor, out var color))
        {
            resources[$"{keyPrefix}Color"] = color;
            resources[$"{keyPrefix}Brush"] = new SolidColorBrush(color);
        }
    }
}
