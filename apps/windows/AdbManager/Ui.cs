// Small helpers for building WinUI in code, and the design tokens.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace AdbManager;

/// Colours from design/tokens.json. Windows keeps the user's system accent.
public static class Palette
{
    private static readonly Dictionary<string, (uint LightFg, uint LightBg, uint DarkFg, uint DarkBg)> Chips = new()
    {
        ["online"] = (0x1A7336, 0xE2F3E6, 0x6BD68B, 0x173222),
        ["attention"] = (0x8A5A00, 0xFBF0D9, 0xF2C14E, 0x3A2E12),
        ["muted"] = (0x5B6765, 0xECEFEE, 0xA3B0AE, 0x262E2D),
        ["recovery"] = (0x7443C9, 0xEFE8FB, 0xC4A6FF, 0x2C2342),
        ["fastboot"] = (0x1F5FBF, 0xE4EDFB, 0x8DB8FF, 0x1B2A44),
        ["error"] = (0xB42318, 0xFCE8E6, 0xFF8A80, 0x3D1D1B),
    };

    public static SolidColorBrush Rgb(uint hex) =>
        new(Microsoft.UI.ColorHelper.FromArgb(255, (byte)(hex >> 16), (byte)(hex >> 8), (byte)hex));

    public static (Brush Fg, Brush Bg) Chip(string kind, bool dark)
    {
        var c = Chips.TryGetValue(kind, out var v) ? v : Chips["muted"];
        return dark ? (Rgb(c.DarkFg), Rgb(c.DarkBg)) : (Rgb(c.LightFg), Rgb(c.LightBg));
    }

    public static string Glyph(string kind) => kind switch
    {
        "online" => "\uE73E",     // CheckMark
        "attention" => "\uE72E",  // Lock
        "recovery" => "\uE90F",   // Repair
        "fastboot" => "\uE945",   // Lightning
        "error" => "\uE711",      // Cancel
        _ => "\uE8CD",            // DisconnectDrive
    };

    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas");
}

/// A state chip: glyph + label + colour, never colour alone. Re-colours
/// itself when the theme changes.
public sealed class Chip : UserControl
{
    private readonly Border _border = new()
    {
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(6, 1, 8, 2),
    };
    private readonly FontIcon _icon = new() { FontSize = 11 };
    private readonly TextBlock _text = new() { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private string _kind = "muted";

    public Chip()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        row.Children.Add(_icon);
        row.Children.Add(_text);
        _border.Child = row;
        Content = _border;
        ActualThemeChanged += (_, _) => Paint();
    }

    public void Set(string label, string kind)
    {
        _text.Text = label;
        _kind = kind;
        _icon.Glyph = Palette.Glyph(kind);
        Paint();
    }

    private void Paint()
    {
        var (fg, bg) = Palette.Chip(_kind, ActualTheme == ElementTheme.Dark);
        _border.Background = bg;
        _icon.Foreground = fg;
        _text.Foreground = fg;
    }
}

public static class Ui
{
    public static TextBlock Text(string text, double size = 14, bool bold = false, bool dim = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        Opacity = dim ? 0.7 : 1,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBlock Title(string text) => Text(text, 20, bold: true);

    public static Button Button(string label, Action onClick, bool accent = false, string? glyph = null)
    {
        object content = label;
        if (glyph is not null)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            sp.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
            sp.Children.Add(new TextBlock { Text = label });
            content = sp;
        }
        var b = new Button { Content = content };
        if (accent) b.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        b.Click += (_, _) => onClick();
        return b;
    }

    public static Button IconButton(string glyph, string tooltip, Action onClick)
    {
        var b = new Button { Content = new FontIcon { Glyph = glyph, FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(b, tooltip);
        b.Click += (_, _) => onClick();
        return b;
    }

    public static StackPanel Row(double spacing, params UIElement[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    public static StackPanel Column(double spacing, params UIElement[] children)
    {
        var p = new StackPanel { Spacing = spacing };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    /// A card: the Windows 11 "layer" surface used for grouped content.
    public static Border Card(UIElement child) => new()
    {
        Child = child,
        Padding = new Thickness(16),
        CornerRadius = new CornerRadius(8),
        BorderThickness = new Thickness(1),
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
    };

    /// Grid with the given column widths ("*", "2*", "auto" or a number).
    public static Grid Columns(params string[] widths)
    {
        var g = new Grid { ColumnSpacing = 12 };
        foreach (var w in widths)
        {
            GridLength len = w switch
            {
                "auto" => GridLength.Auto,
                _ when w.EndsWith('*') => new GridLength(w.Length == 1 ? 1 : double.Parse(w[..^1]), GridUnitType.Star),
                _ => new GridLength(double.Parse(w)),
            };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = len });
        }
        return g;
    }

    public static T At<T>(this T el, int column) where T : FrameworkElement
    {
        Grid.SetColumn(el, column);
        return el;
    }

    public static Grid Put(this Grid g, params FrameworkElement[] children)
    {
        foreach (var c in children) g.Children.Add(c);
        return g;
    }

    public static ScrollViewer Scroll(UIElement content, double maxWidth = 1000) => new()
    {
        Content = new Grid { MaxWidth = maxWidth, Padding = new Thickness(24, 16, 24, 24), Children = { content } },
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };

    public static async Task<bool> Confirm(XamlRoot root, string heading, string body, string verb)
    {
        var d = new ContentDialog
        {
            Title = heading,
            Content = body,
            PrimaryButtonText = verb,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = root,
        };
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }

    public static async Task<string?> Prompt(XamlRoot root, string heading, string body, string placeholder, string verb)
    {
        var box = new TextBox { PlaceholderText = placeholder, FontFamily = Palette.Mono };
        var d = new ContentDialog
        {
            Title = heading,
            Content = Column(8, Text(body, dim: true), box),
            PrimaryButtonText = verb,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };
        return await d.ShowAsync() == ContentDialogResult.Primary && box.Text.Trim().Length > 0 ? box.Text.Trim() : null;
    }

    public static async Task<string?> PickFile(params string[] extensions)
    {
        var p = new FileOpenPicker { ViewMode = PickerViewMode.List };
        foreach (var e in extensions.Length == 0 ? ["*"] : extensions) p.FileTypeFilter.Add(e);
        WinRT.Interop.InitializeWithWindow.Initialize(p, MainWindow.Handle);
        return (await p.PickSingleFileAsync())?.Path;
    }

    public static async Task<string?> PickFolder()
    {
        var p = new FolderPicker();
        p.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(p, MainWindow.Handle);
        return (await p.PickSingleFolderAsync())?.Path;
    }

    public static string Size(ulong n)
    {
        string[] u = ["B", "KB", "MB", "GB", "TB"];
        double v = n;
        var i = 0;
        while (v >= 1000 && i < u.Length - 1) { v /= 1000; i++; }
        return i == 0 ? $"{n} B" : $"{v:0.0} {u[i]}";
    }
}
