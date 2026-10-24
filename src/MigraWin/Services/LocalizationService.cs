using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace MigraWin.Services;

public sealed record LanguageOption(string Code, string CultureName, string NativeName);

public static class LocalizationService
{
    private static readonly ConditionalWeakTable<DependencyObject, Dictionary<string, string>> Originals = new();
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _translations =
        LocalizationCatalog.Load();

    public static IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("it", "it-IT", "Italiano"),
        new("en", "en-US", "English"),
        new("zh", "zh-CN", "简体中文"),
        new("ja", "ja-JP", "日本語"),
        new("ko", "ko-KR", "한국어"),
        new("es", "es-ES", "Español"),
        new("tr", "tr-TR", "Türkçe"),
        new("ru", "ru-RU", "Русский"),
        new("el", "el-GR", "Ελληνικά"),
        new("fr", "fr-FR", "Français"),
        new("de", "de-DE", "Deutsch")
    ];

    public static event Action? LanguageChanged;
    public static string CurrentLanguage { get; private set; } = DetectSystemLanguage();

    static LocalizationService()
    {
        EventManager.RegisterClassHandler(
            typeof(FrameworkElement),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => LocalizeElement((DependencyObject)sender)),
            handledEventsToo: true);
    }

    public static string Translate(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || CurrentLanguage == "it")
        {
            return source;
        }

        if (_translations.TryGetValue(CurrentLanguage, out var language)
            && language.TryGetValue(source, out var translated))
        {
            return translated;
        }

        if (CurrentLanguage != "en"
            && _translations.TryGetValue("en", out var english)
            && english.TryGetValue(source, out translated))
        {
            return translated;
        }

        return source;
    }

    public static string Format(string source, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Translate(source), arguments);

    public static void SetLanguage(string code)
    {
        var option = Languages.FirstOrDefault(item =>
                         item.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                     ?? Languages.First(item => item.Code == DetectSystemLanguage());
        if (option.Code == CurrentLanguage)
        {
            return;
        }

        CurrentLanguage = option.Code;
        var culture = CultureInfo.GetCultureInfo(option.CultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        LanguageChanged?.Invoke();
    }

    public static int GetLanguageIndex(string code)
    {
        for (var index = 0; index < Languages.Count; index++)
        {
            if (Languages[index].Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    public static string DetectSystemLanguage()
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return Languages.Any(item => item.Code == language) ? language : "it";
    }

    public static void LocalizeTree(DependencyObject root)
    {
        var visited = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        Visit(root, visited);
    }

    private static void Visit(DependencyObject element, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(element))
        {
            return;
        }

        LocalizeElement(element);
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
        {
            Visit(child, visited);
        }

        if (element is not System.Windows.Media.Visual)
        {
            return;
        }

        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); index++)
        {
            Visit(System.Windows.Media.VisualTreeHelper.GetChild(element, index), visited);
        }
    }

    private static void LocalizeElement(DependencyObject element)
    {
        switch (element)
        {
            case TextBlock textBlock when !BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty):
                SetTranslated(textBlock, TextBlock.TextProperty, "text", textBlock.Text);
                break;
            case Run run when !BindingOperations.IsDataBound(run, Run.TextProperty):
                SetTranslated(run, Run.TextProperty, "text", run.Text);
                break;
            case ContentControl control
                when control.Content is string content
                     && !BindingOperations.IsDataBound(control, ContentControl.ContentProperty):
                SetTranslated(control, ContentControl.ContentProperty, "content", content);
                break;
            case Window window when !BindingOperations.IsDataBound(window, Window.TitleProperty):
                SetTranslated(window, Window.TitleProperty, "title", window.Title);
                break;
        }

        if (element is FrameworkElement frameworkElement
            && frameworkElement.ToolTip is string toolTip
            && !BindingOperations.IsDataBound(frameworkElement, FrameworkElement.ToolTipProperty))
        {
            SetTranslated(
                frameworkElement,
                FrameworkElement.ToolTipProperty,
                "tooltip",
                toolTip);
        }

        var automationName = AutomationProperties.GetName(element);
        if (!string.IsNullOrWhiteSpace(automationName)
            && !BindingOperations.IsDataBound(element, AutomationProperties.NameProperty))
        {
            SetTranslated(
                element,
                AutomationProperties.NameProperty,
                "automation",
                automationName);
        }
    }

    private static void SetTranslated(
        DependencyObject element,
        DependencyProperty property,
        string key,
        string current)
    {
        var originals = Originals.GetOrCreateValue(element);
        if (!originals.TryGetValue(key, out var original))
        {
            original = current;
            originals[key] = original;
        }

        var translated = Translate(original);
        if (!Equals(element.GetValue(property), translated))
        {
            element.SetCurrentValue(property, translated);
        }
    }
}
