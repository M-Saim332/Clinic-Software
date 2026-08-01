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

    // ── TopBar ──────────────────────────────────────────────────────────────
    public string TopBarBackground { get; set; } = string.Empty;
    public string TopBarForeground { get; set; } = string.Empty;

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
        // ── 1. System Default ───────────────────────────────────────
        new ThemeDefinition
        {
            Name                   = "System Default",
            IsDark                 = false,
            SidebarBackground      = "#FFFFFF",
            SidebarForeground      = "#4B5563",
            SidebarHoverBackground = "#F3F4F6",
            SidebarHoverForeground = "#111827",
            SidebarActiveBackground= "#1D4ED8",
            SidebarActiveForeground= "#FFFFFF",
            TopBarBackground       = "#FFFFFF",
            TopBarForeground       = "#4B5563",
            Primary                = "#1D4ED8",
            PrimaryHover           = "#1E40AF",
            PrimaryPressed         = "#1E3A8A",
            PrimaryLight           = "#DBEAFE",
            ContentBackground      = "#F8FAFC",
            CardBackground         = "#FFFFFF",
            ContentForeground      = "#111827",
            SubtleForeground       = "#6B7280",
            BorderColor            = "#E5E7EB",
            InputBackground        = "#FFFFFF"
        }
    };

    /// <summary>Runtime list of admin-created custom themes (loaded from DB on startup).</summary>
    public static System.Collections.ObjectModel.ObservableCollection<ThemeDefinition> CustomThemes { get; private set; } = new();

    /// <summary>All themes: built-in + custom.</summary>
    public static IEnumerable<ThemeDefinition> AllThemes =>
        BuiltInThemes.Concat(CustomThemes);

    public static string CurrentThemeName { get; private set; } = "System Default";

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

        // TopBar
        SetColorAndBrush(res, "ThemeTopBarBackground", theme.TopBarBackground);
        SetColorAndBrush(res, "ThemeTopBarForeground", theme.TopBarForeground);

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
        var existing = CustomThemes.FirstOrDefault(t => t.Name == theme.Name);
        if (existing != null)
        {
            CustomThemes.Remove(existing);
        }
        CustomThemes.Add(theme);

        PersistCustomThemes();
    }

    public static void DeleteCustomTheme(string themeName)
    {
        var existing = CustomThemes.FirstOrDefault(t => t.Name == themeName);
        if (existing != null)
        {
            CustomThemes.Remove(existing);
        }
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
                    CustomThemes.Clear();
                    foreach (var t in list)
                    {
                        t.IsCustom = true;
                        CustomThemes.Add(t);
                    }
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

        ApplyTheme("System Default");
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
