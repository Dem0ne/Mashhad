using Mashhad.Core.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Mashhad.App;

/// <summary>Current UI strings (Arabic / English).</summary>
public static class Loc
{
    public static Localizer Current { get; private set; } = new("en");

    public static void SetLanguage(string settingValue)
    {
        Current = new Localizer(Localizer.ResolveLanguage(settingValue));
        L.Refresh();
    }

    public static string T(string key) => Current.Get(key);
    public static string F(string key, params object?[] args) => Current.Format(key, args);
}

/// <summary>
/// Attached properties that localise XAML elements: <c>local:L.Text="Key"</c> sets the visible
/// text, <c>local:L.Tip="Key"</c> the tooltip and accessible name. Elements are tracked weakly so
/// the language can be switched at runtime.
/// </summary>
public static class L
{
    private static readonly List<WeakReference<DependencyObject>> Targets = new();

    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(L), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder", typeof(string), typeof(L), new PropertyMetadata(null, OnChanged));

    public static string GetPlaceholder(DependencyObject o) => (string)o.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject o, string value) => o.SetValue(PlaceholderProperty, value);

    public static readonly DependencyProperty TipProperty = DependencyProperty.RegisterAttached(
        "Tip", typeof(string), typeof(L), new PropertyMetadata(null, OnChanged));

    public static string GetText(DependencyObject o) => (string)o.GetValue(TextProperty);
    public static void SetText(DependencyObject o, string value) => o.SetValue(TextProperty, value);
    public static string GetTip(DependencyObject o) => (string)o.GetValue(TipProperty);
    public static void SetTip(DependencyObject o, string value) => o.SetValue(TipProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!Targets.Exists(w => w.TryGetTarget(out var t) && ReferenceEquals(t, d)))
            Targets.Add(new WeakReference<DependencyObject>(d));
        Apply(d);
    }

    public static void Refresh()
    {
        Targets.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var w in Targets.ToArray())
            if (w.TryGetTarget(out var d)) Apply(d);
    }

    private static void Apply(DependencyObject d)
    {
        if (d.GetValue(TextProperty) is string textKey)
        {
            string s = Loc.T(textKey);
            switch (d)
            {
                case TextBlock t: t.Text = s; break;
                case MenuFlyoutSubItem sub: sub.Text = s; break;
                case MenuFlyoutItem item: item.Text = s; break;
                case ToggleSwitch ts: ts.Header = s; break;
                case NumberBox nb: nb.Header = s; break;
                case ComboBox cb: cb.Header = s; break;
                case ContentControl cc: cc.Content = s; break;
            }
        }
        if (d.GetValue(PlaceholderProperty) is string phKey && d is TextBox box)
            box.PlaceholderText = Loc.T(phKey);
        if (d.GetValue(TipProperty) is string tipKey)
        {
            string s = Loc.T(tipKey);
            ToolTipService.SetToolTip(d, s);
            AutomationProperties.SetName(d, s);
        }
    }
}
