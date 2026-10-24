using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MigraWin.Services;
using MigraWin.ViewModels;

namespace MigraWin;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        SourceInitialized += (_, _) => WindowAppearance.Apply(this, ThemeManager.CurrentPreference);
        ThemeManager.ThemeChanged += ThemeManager_OnThemeChanged;
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e) =>
        await _viewModel.InitializeAsync();

    private void Window_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 1100;
        NavigationColumn.Width = new GridLength(compact ? 76 : 238);
        SidebarFooter.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _viewModel.IsCompactNavigation = compact;
    }

    private async void Window_OnClosed(object? sender, EventArgs e)
    {
        ThemeManager.ThemeChanged -= ThemeManager_OnThemeChanged;
        await _viewModel.DisposeAsync();
    }

    private void ThemeManager_OnThemeChanged(ThemePreference preference)
    {
        if (IsLoaded)
        {
            WindowAppearance.Apply(this, preference);
        }
    }
}

internal static class WindowAppearance
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmSystemBackdropType = 38;

    public static void Apply(Window window, ThemePreference preference)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        var enabled = preference switch
        {
            ThemePreference.Dark => 1,
            ThemePreference.Light => 0,
            _ => IsLightTheme() ? 0 : 1
        };
        DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
        var corner = 2;
        DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref corner, sizeof(int));
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            var backdrop = 2;
            DwmSetWindowAttribute(handle, DwmSystemBackdropType, ref backdrop, sizeof(int));
        }
    }

    private static bool IsLightTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) != 0;
        }
        catch
        {
            return true;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
