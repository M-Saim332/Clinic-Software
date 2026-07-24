using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ClinicSystem.Data.Repositories;

namespace ClinicSystem.UI.Services;

public class ThemeDefinition
{
    public string Name { get; set; } = string.Empty;
    public string SidebarBackground { get; set; } = string.Empty;
    public string SidebarForeground { get; set; } = string.Empty;
    public string SidebarHoverBackground { get; set; } = string.Empty;
    public string SidebarHoverForeground { get; set; } = string.Empty;
    public string SidebarActiveBackground { get; set; } = string.Empty;
    public string SidebarActiveForeground { get; set; } = string.Empty;
    
    public string Primary { get; set; } = string.Empty;
    public string PrimaryHover { get; set; } = string.Empty;
    public string PrimaryPressed { get; set; } = string.Empty;
    public string PrimaryLight { get; set; } = string.Empty;
}

public class ThemeService
{
    public static readonly List<ThemeDefinition> AvailableThemes = new()
    {
        new ThemeDefinition
        {
            Name = "Modern Blue",
            SidebarBackground = "#0F172A",
            SidebarForeground = "#94A3B8",
            SidebarHoverBackground = "#1E293B",
            SidebarHoverForeground = "#F8FAFC",
            SidebarActiveBackground = "#1E293B",
            SidebarActiveForeground = "#38BDF8",
            Primary = "#2563EB",
            PrimaryHover = "#1D4ED8",
            PrimaryPressed = "#1E40AF",
            PrimaryLight = "#EFF6FF"
        },
        new ThemeDefinition
        {
            Name = "Slate & Indigo",
            SidebarBackground = "#1E293B",
            SidebarForeground = "#94A3B8",
            SidebarHoverBackground = "#334155",
            SidebarHoverForeground = "#F8FAFC",
            SidebarActiveBackground = "#334155",
            SidebarActiveForeground = "#818CF8",
            Primary = "#4F46E5",
            PrimaryHover = "#4338CA",
            PrimaryPressed = "#3730A3",
            PrimaryLight = "#EEF2FF"
        },
        new ThemeDefinition
        {
            Name = "Teal & Mint",
            SidebarBackground = "#134E4A",
            SidebarForeground = "#99F6E4",
            SidebarHoverBackground = "#0F766E",
            SidebarHoverForeground = "#F0FDFA",
            SidebarActiveBackground = "#0F766E",
            SidebarActiveForeground = "#5EEAD4",
            Primary = "#0D9488",
            PrimaryHover = "#0F766E",
            PrimaryPressed = "#115E59",
            PrimaryLight = "#F0FDFA"
        },
        new ThemeDefinition
        {
            Name = "Purple & White",
            SidebarBackground = "#3B0764",
            SidebarForeground = "#E9D5FF",
            SidebarHoverBackground = "#581C87",
            SidebarHoverForeground = "#FAF5FF",
            SidebarActiveBackground = "#581C87",
            SidebarActiveForeground = "#D8B4FE",
            Primary = "#7E22CE",
            PrimaryHover = "#6B21A8",
            PrimaryPressed = "#581C87",
            PrimaryLight = "#FAF5FF"
        },
        new ThemeDefinition
        {
            Name = "Amber & White",
            SidebarBackground = "#FFFFFF",
            SidebarForeground = "#4B5563",
            SidebarHoverBackground = "#FEF3C7",
            SidebarHoverForeground = "#D97706",
            SidebarActiveBackground = "#FFFBEB",
            SidebarActiveForeground = "#D97706",
            Primary = "#D97706",
            PrimaryHover = "#B45309",
            PrimaryPressed = "#92400E",
            PrimaryLight = "#FFFBEB"
        },
        new ThemeDefinition
        {
            Name = "Minimal Light",
            SidebarBackground = "#FFFFFF",
            SidebarForeground = "#4B5563",
            SidebarHoverBackground = "#F1F5F9",
            SidebarHoverForeground = "#2563EB",
            SidebarActiveBackground = "#EFF6FF",
            SidebarActiveForeground = "#2563EB",
            Primary = "#2563EB",
            PrimaryHover = "#1D4ED8",
            PrimaryPressed = "#1E40AF",
            PrimaryLight = "#EFF6FF"
        }
    };

    public static string CurrentThemeName { get; private set; } = "Slate & Indigo";

    public static void ApplyTheme(string themeName)
    {
        var theme = AvailableThemes.FirstOrDefault(t => t.Name == themeName);
        if (theme == null) return;

        CurrentThemeName = themeName;
        
        var appResources = Application.Current?.Resources;
        if (appResources == null) return;

        SetColorAndBrush(appResources, "ThemeSidebarBackground", theme.SidebarBackground);
        SetColorAndBrush(appResources, "ThemeSidebarForeground", theme.SidebarForeground);
        SetColorAndBrush(appResources, "ThemeSidebarHoverBackground", theme.SidebarHoverBackground);
        SetColorAndBrush(appResources, "ThemeSidebarHoverForeground", theme.SidebarHoverForeground);
        SetColorAndBrush(appResources, "ThemeSidebarActiveBackground", theme.SidebarActiveBackground);
        SetColorAndBrush(appResources, "ThemeSidebarActiveForeground", theme.SidebarActiveForeground);

        SetColorAndBrush(appResources, "ThemePrimary", theme.Primary);
        SetColorAndBrush(appResources, "ThemePrimaryHover", theme.PrimaryHover);
        SetColorAndBrush(appResources, "ThemePrimaryPressed", theme.PrimaryPressed);
        SetColorAndBrush(appResources, "ThemePrimaryLight", theme.PrimaryLight);
    }

    private static void SetColorAndBrush(IResourceDictionary resources, string keyPrefix, string hexColor)
    {
        if (Color.TryParse(hexColor, out var color))
        {
            resources[$"{keyPrefix}Color"] = color;
            resources[$"{keyPrefix}Brush"] = new SolidColorBrush(color);
        }
    }

    public static void Initialize(SettingsRepository repo)
    {
        try
        {
            var dict = repo.GetAll();
            if (dict.TryGetValue("AppTheme", out var savedTheme))
            {
                if (AvailableThemes.Any(t => t.Name == savedTheme))
                {
                    ApplyTheme(savedTheme);
                    return;
                }
            }
            // Default to Slate & Indigo
            ApplyTheme("Slate & Indigo");
        }
        catch
        {
            ApplyTheme("Slate & Indigo");
        }
    }
}
