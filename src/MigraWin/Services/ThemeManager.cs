using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace MigraWin.Services;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public static class ThemeManager
{
    public static event Action<ThemePreference>? ThemeChanged;

    public static ThemePreference CurrentPreference { get; private set; } = ThemePreference.System;

    public static void Apply(ThemePreference preference)
    {
        CurrentPreference = preference;
        var resources = Application.Current.Resources;
        if (SystemParameters.HighContrast)
        {
            Set(resources, "AppWindowBrush", SystemColors.WindowBrush);
            Set(resources, "AppBaseBrush", SystemColors.ControlBrush);
            Set(resources, "AppSurfaceBrush", SystemColors.WindowBrush);
            Set(resources, "AppSurfaceAltBrush", SystemColors.ControlBrush);
            Set(resources, "AppStrokeBrush", SystemColors.ControlDarkBrush);
            Set(resources, "AppTextPrimaryBrush", SystemColors.WindowTextBrush);
            Set(resources, "AppTextSecondaryBrush", SystemColors.GrayTextBrush);
            Set(resources, "AppAccentBrush", SystemColors.HighlightBrush);
            Set(resources, "AppAccentTextBrush", SystemColors.HighlightTextBrush);
            Set(resources, "AppSelectionBrush", SystemColors.HighlightBrush);
            Set(resources, "AppSuccessBrush", SystemColors.HighlightBrush);
            Set(resources, "AppWarningBrush", SystemColors.HighlightBrush);
            Set(resources, "AppDangerBrush", SystemColors.HighlightBrush);
            return;
        }

        var dark = preference == ThemePreference.Dark
                   || preference == ThemePreference.System && !IsSystemLightMode();
        var accent = ReadAccentColor(dark);
        if (dark)
        {
            Set(resources, "AppWindowBrush", Brush(32, 32, 32));
            Set(resources, "AppBaseBrush", Brush(40, 40, 40));
            Set(resources, "AppSurfaceBrush", Brush(45, 45, 45));
            Set(resources, "AppSurfaceAltBrush", Brush(54, 54, 54));
            Set(resources, "AppStrokeBrush", Brush(75, 75, 75));
            Set(resources, "AppTextPrimaryBrush", Brush(255, 255, 255));
            Set(resources, "AppTextSecondaryBrush", Brush(190, 190, 190));
            Set(resources, "AppAccentTextBrush", Brushes.White);
            Set(resources, "AppSelectionBrush", WithAlpha(accent, 74));
            Set(resources, "AppSuccessBrush", Brush(82, 196, 126));
            Set(resources, "AppWarningBrush", Brush(246, 189, 96));
            Set(resources, "AppDangerBrush", Brush(255, 119, 119));
        }
        else
        {
            Set(resources, "AppWindowBrush", Brush(243, 243, 243));
            Set(resources, "AppBaseBrush", Brush(249, 249, 249));
            Set(resources, "AppSurfaceBrush", Brush(255, 255, 255));
            Set(resources, "AppSurfaceAltBrush", Brush(245, 246, 248));
            Set(resources, "AppStrokeBrush", Brush(218, 221, 225));
            Set(resources, "AppTextPrimaryBrush", Brush(28, 28, 30));
            Set(resources, "AppTextSecondaryBrush", Brush(93, 97, 104));
            Set(resources, "AppAccentTextBrush", Brushes.White);
            Set(resources, "AppSelectionBrush", WithAlpha(accent, 38));
            Set(resources, "AppSuccessBrush", Brush(26, 127, 79));
            Set(resources, "AppWarningBrush", Brush(154, 91, 0));
            Set(resources, "AppDangerBrush", Brush(196, 43, 28));
        }

        Set(resources, "AppAccentBrush", new SolidColorBrush(accent));
        try
        {
            Application.Current.ThemeMode = preference switch
            {
                ThemePreference.Dark => ThemeMode.Dark,
                ThemePreference.Light => ThemeMode.Light,
                _ => ThemeMode.System
            };
        }
        catch (InvalidOperationException)
        {
            // Older Windows builds keep the system control theme; custom tokens still apply.
        }

        ThemeChanged?.Invoke(preference);
    }

    private static bool IsSystemLightMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static Color ReadAccentColor(bool dark)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            var raw = Convert.ToUInt32(key?.GetValue("AccentColor", 0));
            if (raw != 0)
            {
                var color = Color.FromRgb((byte)(raw & 0xFF), (byte)((raw >> 8) & 0xFF), (byte)((raw >> 16) & 0xFF));
                return dark ? Lighten(color, 28) : color;
            }
        }
        catch
        {
        }

        return Color.FromRgb(0, 120, 212);
    }

    private static Color Lighten(Color color, byte amount) =>
        Color.FromRgb(
            (byte)Math.Min(255, color.R + amount),
            (byte)Math.Min(255, color.G + amount),
            (byte)Math.Min(255, color.B + amount));

    private static SolidColorBrush Brush(byte red, byte green, byte blue) =>
        new(Color.FromRgb(red, green, blue));

    private static SolidColorBrush WithAlpha(Color color, byte alpha) =>
        new(Color.FromArgb(alpha, color.R, color.G, color.B));

    private static void Set(ResourceDictionary resources, string key, Brush brush) => resources[key] = brush;
}
