// Main window: NavigationView on Mica, CommandBar with the targets pill and
// fleet actions, InfoBar for job outcomes.

using AdbManager.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AdbManager;

public sealed class MainWindow : Window
{
    public static IntPtr Handle { get; private set; }

    private readonly AppState? _state;
    private readonly NavigationView _nav = new();
    private readonly InfoBar _banner = new() { IsClosable = true, Margin = new Thickness(24, 0, 24, 8) };
    private readonly TextBlock _targets = new() { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _targetsPill = new() { CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 3, 10, 4), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
    private readonly TextBlock _pageTitle = Ui.Title("Devices");
    private readonly ContentControl _content = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly AppBarButton _reboot = new() { Label = "Reboot", Icon = new FontIcon { Glyph = "\uE72C" } };
    private readonly Dictionary<string, FrameworkElement> _pages = [];
    private readonly Dictionary<string, InfoBadge> _badges = [];
    private readonly DispatcherQueueTimer _bannerTimer;
    private AppsPage? _apps;

    private static readonly (string Tag, string Title, string Glyph)[] Sections =
    [
        ("devices", "Devices", "\uE8EA"),
        ("apps", "Apps", "\uE71D"),
        ("files", "Files", "\uE8B7"),
        ("shell", "Shell", "\uE756"),
        ("routines", "Routines", "\uE8FD"),
        ("activity", "Activity", "\uE9D9"),
    ];

    public MainWindow()
    {
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Title = "ADB Manager";
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1240, 780));

        var dq = DispatcherQueue.GetForCurrentThread();
        _bannerTimer = dq.CreateTimer();
        _bannerTimer.Interval = TimeSpan.FromSeconds(6);
        _bannerTimer.IsRepeating = false;
        _bannerTimer.Tick += (_, _) => _banner.IsOpen = false;

        var titleBar = new Grid { Height = 48, Padding = new Thickness(16, 0, 0, 0) };
        titleBar.Children.Add(Ui.Row(12, new FontIcon { Glyph = "\uE8EA", FontSize = 16 }, Ui.Text("ADB Manager", 12)));
        SetTitleBar(titleBar);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(titleBar);
        Content = root;

        try
        {
            _state = new AppState(AppState.LoadConfig(), a => dq.TryEnqueue(() => a()));
        }
        catch (Exception e)
        {
            var err = Ui.Column(12, Ui.Title("ADB Manager could not start"), Ui.Text(e.Message, dim: true));
            err.Margin = new Thickness(48);
            Grid.SetRow(err, 1);
            root.Children.Add(err);
            return;
        }
        var state = _state;
        Closed += (_, _) => state.Dispose();

        foreach (var (tag, title, glyph) in Sections)
        {
            var badge = new InfoBadge { Visibility = Visibility.Collapsed };
            _badges[tag] = badge;
            _nav.MenuItems.Add(new NavigationViewItem { Content = title, Tag = tag, Icon = new FontIcon { Glyph = glyph }, InfoBadge = badge });
        }
        _nav.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
        _nav.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
        _nav.IsSettingsVisible = true;
        _nav.OpenPaneLength = 220;
        _nav.SelectionChanged += (_, e) =>
        {
            if (e.IsSettingsSelected) Show("settings", "Settings");
            else if (e.SelectedItem is NavigationViewItem { Tag: string tag }) Show(tag, Sections.First(s => s.Tag == tag).Title);
        };

        // Header: title, targets pill, fleet actions.
        var bar = new CommandBar { DefaultLabelPosition = CommandBarDefaultLabelPosition.Right, VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        var connect = new AppBarButton { Label = "Connect…", Icon = new FontIcon { Glyph = "\uE968" } };
        connect.Click += async (_, _) => await ConnectAsync();
        var menu = new MenuFlyout();
        foreach (var mode in new[] { "system", "recovery", "bootloader", "fastboot", "sideload" })
        {
            var item = new MenuFlyoutItem { Text = Step.Cap(mode) };
            item.Click += async (_, _) => await RebootAsync(mode);
            menu.Items.Add(item);
        }
        _reboot.Flyout = menu;
        _reboot.IsEnabled = false;
        var install = new AppBarButton { Label = "Install APK…", Icon = new FontIcon { Glyph = "\uE896" } };
        install.Click += async (_, _) => { if (await Ui.PickFile(".apk") is { } p) OpenApk(p); };
        bar.PrimaryCommands.Add(connect);
        bar.PrimaryCommands.Add(_reboot);
        bar.PrimaryCommands.Add(install);
        _targetsPill.Child = _targets;

        var header = Ui.Columns("auto", "auto", "*").Put(_pageTitle.At(0), _targetsPill.At(1), bar.At(2));
        header.Padding = new Thickness(24, 8, 12, 8);

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_banner, 1);
        Grid.SetRow(_content, 2);
        body.Children.Add(header);
        body.Children.Add(_banner);
        body.Children.Add(_content);
        _nav.Content = body;

        Grid.SetRow(_nav, 1);
        root.Children.Add(_nav);

        // Drop an APK anywhere to install it.
        root.AllowDrop = true;
        root.DragOver += (_, e) => e.AcceptedOperation = DataPackageOperation.Copy;
        root.Drop += async (_, e) =>
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var items = await e.DataView.GetStorageItemsAsync();
            var apk = items.OfType<StorageFile>().FirstOrDefault(f => f.FileType.Equals(".apk", StringComparison.OrdinalIgnoreCase));
            if (apk is null) ShowBanner("Drop an .apk file to install it.", true);
            else OpenApk(apk.Path);
        };

        state.Banner += ShowBanner;
        state.TargetsChanged += UpdateTargets;
        state.Devices.CollectionChanged += (_, _) => SetBadge("devices", state.Devices.Count);
        state.Jobs.CollectionChanged += (_, _) => SetBadge("activity", state.RunningJobs);
        state.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppState.RunningJobs)) SetBadge("activity", state.RunningJobs); };
        root.ActualThemeChanged += (_, _) => UpdateTargets();
        UpdateTargets();
        _nav.SelectedItem = _nav.MenuItems[0];
    }

    private void Show(string tag, string title)
    {
        if (_state is null) return;
        if (!_pages.TryGetValue(tag, out var page))
        {
            page = tag switch
            {
                "devices" => new DevicesPage(_state, Go),
                "apps" => _apps = new AppsPage(_state, Go),
                "files" => new FilesPage(_state),
                "shell" => new ShellPage(_state),
                "routines" => new RoutinesPage(_state, Go),
                "activity" => new ActivityPage(_state),
                _ => new SettingsPage(_state),
            };
            _pages[tag] = page;
        }
        _pageTitle.Text = title;
        _content.Content = page;
    }

    /// Navigate to a section by tag.
    private void Go(string tag)
    {
        var item = _nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == tag);
        if (item is not null) _nav.SelectedItem = item;
    }

    private void OpenApk(string path)
    {
        Go("apps");
        _apps?.SetApk(path);
    }

    private void SetBadge(string tag, int n)
    {
        if (!_badges.TryGetValue(tag, out var b)) return;
        b.Value = n;
        b.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateTargets()
    {
        if (_state is null) return;
        var n = _state.Targets.Count;
        _targets.Text = _state.TargetsLabel;
        var dark = _targetsPill.ActualTheme == ElementTheme.Dark;
        _targetsPill.Background = n == 0
            ? (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
            : Palette.Rgb(dark ? 0x123A33u : 0xE3F4F0u);
        _targets.Foreground = n == 0
            ? (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            : Palette.Rgb(dark ? 0x3DD6B5u : 0x0F7B6Cu);
        _reboot.IsEnabled = n > 0;
    }

    private void ShowBanner(string text, bool problem)
    {
        _banner.Message = text;
        _banner.Severity = problem ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
        _banner.IsOpen = true;
        _bannerTimer.Stop();
        if (!problem) _bannerTimer.Start();
    }

    private async Task RebootAsync(string mode)
    {
        if (_state is null) return;
        var n = _state.Targets.Count;
        var ok = await Ui.Confirm(Content.XamlRoot, $"Reboot {AppState.Count(n, "device", "devices")} to {Step.Cap(mode)}?",
            "Running apps and transfers on these devices are interrupted.", "Reboot");
        if (ok) _state.RunVisible(Cmd.Of("reboot", ("serials", _state.Targets), ("mode", mode)));
    }

    private async Task ConnectAsync()
    {
        if (_state is null) return;
        var address = await Ui.Prompt(Content.XamlRoot, "Connect over network",
            "Enable wireless or TCP debugging on the device, then enter its address.", "192.168.1.42:5555", "Connect");
        if (address is not null) _state.RunVisible(Cmd.Of("connect", ("address", address)));
    }
}
