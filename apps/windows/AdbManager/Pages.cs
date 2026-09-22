// The sections of the app. Each page is plain WinUI controls built in code.

using System.Collections.Specialized;
using AdbManager.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace AdbManager;

// ------------------------------------------------------------------ Devices

public sealed class DevicesPage : Grid
{
    private readonly AppState _s;
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Extended };
    private readonly Dictionary<string, (Grid Row, TextBlock Name, TextBlock Serial, Chip Chip, TextBlock Android, TextBlock Battery)> _rows = [];
    private readonly StackPanel _empty = new() { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 460 };
    private readonly TextBlock _hint = Ui.Text("", 13, dim: true);
    private readonly Inspector _inspector;
    private bool _syncing;

    public DevicesPage(AppState s, Action<string> go)
    {
        _s = s;
        _inspector = new Inspector(s, go);
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        ColumnSpacing = 16;
        Padding = new Thickness(24, 0, 24, 24);

        var header = RowGrid();
        header.Put(H("Device").At(0), H("Serial").At(1), H("State").At(2), H("Android").At(3), H("Battery").At(4));
        header.Padding = new Thickness(16, 0, 16, 6);

        var table = new Grid();
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_list, 1);
        Grid.SetRow(_hint, 2);
        _hint.Margin = new Thickness(16, 8, 16, 0);
        table.Children.Add(header);
        table.Children.Add(_list);
        table.Children.Add(_hint);
        var card = Ui.Card(table);
        card.Padding = new Thickness(0, 12, 0, 12);

        var left = new Grid();
        left.Children.Add(card);
        left.Children.Add(_empty);
        Children.Add(left);
        Children.Add(_inspector.At(1));

        _list.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            s.SetTargets(_list.SelectedItems.OfType<Grid>().Select(g => (string)g.Tag));
        };
        s.Devices.CollectionChanged += (_, _) => Rebuild();
        s.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppState.Server)) UpdateEmpty(); };
        Rebuild();
    }

    private static Grid RowGrid() => Ui.Columns("2*", "*", "130", "120", "70");

    private static TextBlock H(string t)
    {
        var tb = Ui.Text(t, 12, dim: true);
        return tb;
    }

    private void Rebuild()
    {
        _syncing = true;
        _list.Items.Clear();
        foreach (var d in _s.Devices)
        {
            if (!_rows.TryGetValue(d.Serial, out var r))
            {
                var name = Ui.Text("", 14, bold: true);
                var serial = new TextBlock { FontFamily = Palette.Mono, FontSize = 12, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center };
                var chip = new Chip();
                var android = Ui.Text("");
                var battery = Ui.Text("");
                var row = RowGrid().Put(name.At(0), serial.At(1), chip.At(2), android.At(3), battery.At(4));
                row.Tag = d.Serial;
                row.MinHeight = 40;
                r = (row, name, serial, chip, android, battery);
                _rows[d.Serial] = r;
                d.PropertyChanged += (_, _) => Fill(d);
            }
            _list.Items.Add(r.Row);
            Fill(d);
        }
        foreach (var gone in _rows.Keys.Where(k => _s.Devices.All(d => d.Serial != k)).ToList()) _rows.Remove(gone);
        foreach (var t in _s.Targets)
            if (_rows.TryGetValue(t, out var r)) _list.SelectedItems.Add(r.Row);
        _syncing = false;
        var hints = _s.Devices.Where(d => d.StateHint is not null).Select(d => $"{d.DisplayName}: {d.StateHint}").ToList();
        _hint.Text = string.Join("\n", hints);
        _hint.Visibility = hints.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateEmpty();
        _inspector.Update();
    }

    private void Fill(DeviceItem d)
    {
        if (!_rows.TryGetValue(d.Serial, out var r)) return;
        r.Name.Text = d.DisplayName;
        r.Serial.Text = d.Serial;
        r.Chip.Set(d.StateLabel, d.StateKind);
        r.Android.Text = d.AndroidText;
        r.Battery.Text = d.BatteryText;
        _inspector.Update();
    }

    private void UpdateEmpty()
    {
        var has = _s.Devices.Count > 0;
        _list.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        _empty.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        _empty.Children.Clear();
        if (has) return;
        switch (_s.Server.State)
        {
            case "adb_not_found":
                _empty.Children.Add(Ui.Title("adb was not found"));
                _empty.Children.Add(Ui.Text("Install Android platform-tools, or choose adb.exe.", dim: true));
                _empty.Children.Add(Ui.Button("Choose adb.exe…", async () =>
                {
                    if (await Ui.PickFile(".exe") is not { } p) return;
                    var c = _s.Config.Clone();
                    c.AdbPath = p;
                    if (_s.SaveConfig(c)) AppState.StoreConfig(c);
                }, accent: true));
                break;
            case "error":
                _empty.Children.Add(Ui.Title("adb is not responding"));
                _empty.Children.Add(Ui.Text(_s.Server.Message ?? "", dim: true));
                _empty.Children.Add(Ui.Button("Restart adb", () => _s.RunVisible(Cmd.Of("restart_server"))));
                break;
            case "running":
                _empty.Children.Add(Ui.Title("No devices"));
                _empty.Children.Add(Ui.Text("Connect a device with USB debugging enabled, or connect over the network. On Windows, some devices need their OEM USB driver.", dim: true));
                break;
            default:
                _empty.Children.Add(new ProgressRing { IsActive = true });
                _empty.Children.Add(Ui.Text("Starting adb…", dim: true));
                break;
        }
    }
}

public sealed class Inspector : Grid
{
    private readonly AppState _s;
    private readonly TextBlock _title = Ui.Text("", 18, bold: true);
    private readonly TextBlock _subtitle = new() { FontFamily = Palette.Mono, FontSize = 12, Opacity = 0.7, IsTextSelectionEnabled = true };
    private readonly Chip _chip = new();
    private readonly TextBlock _android = Ui.Text(""), _build = Ui.Text(""), _battery = Ui.Text(""), _link = Ui.Text(""), _imei = Ui.Text("");
    private readonly Button _readImei;
    private readonly StackPanel _details;
    private readonly StackPanel _none;

    public Inspector(AppState s, Action<string> go)
    {
        _s = s;
        _readImei = Ui.Button("Read", ReadImei);
        var props = new Grid { RowSpacing = 10, ColumnSpacing = 12 };
        props.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        props.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var rows = new (string, FrameworkElement)[]
        {
            ("State", _chip), ("Android", _android), ("Build", _build), ("Battery", _battery), ("Connection", _link),
            ("IMEI", Ui.Row(8, _imei, _readImei)),
        };
        for (var i = 0; i < rows.Length; i++)
        {
            props.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = Ui.Text(rows[i].Item1, 13, dim: true);
            Grid.SetRow(label, i);
            Grid.SetRow(rows[i].Item2, i);
            Grid.SetColumn(rows[i].Item2, 1);
            props.Children.Add(label);
            props.Children.Add(rows[i].Item2);
        }
        var actions = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        actions.RowDefinitions.Add(new RowDefinition());
        actions.RowDefinitions.Add(new RowDefinition());
        var buttons = new[]
        {
            Ui.Button("Shell", () => go("shell"), glyph: "\uE756"),
            Ui.Button("Files", () => go("files"), glyph: "\uE8B7"),
            Ui.Button("Recovery", () => s.RunVisible(Cmd.Of("reboot", ("serials", s.Targets), ("mode", "recovery"))), glyph: "\uE90F"),
            Ui.Button("Bootloader", () => s.RunVisible(Cmd.Of("reboot", ("serials", s.Targets), ("mode", "bootloader"))), glyph: "\uE945"),
        };
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(buttons[i], i % 2);
            Grid.SetRow(buttons[i], i / 2);
            actions.Children.Add(buttons[i]);
        }
        _details = Ui.Column(14, Ui.Column(2, _title, _subtitle), props, Ui.Text("Quick actions", 14, bold: true), actions);
        _none = Ui.Column(8, Ui.Text("No device selected", 16, bold: true),
            Ui.Text("Select one or more devices. Actions apply to every selected device.", dim: true));
        var stack = new Grid();
        stack.Children.Add(_details);
        stack.Children.Add(_none);
        Children.Add(Ui.Card(stack));
        s.TargetsChanged += Update;
        Update();
    }

    public void Update()
    {
        var d = _s.Focused;
        _details.Visibility = d is null ? Visibility.Collapsed : Visibility.Visible;
        _none.Visibility = d is null ? Visibility.Visible : Visibility.Collapsed;
        if (d is null) return;
        var more = _s.Targets.Count - 1;
        _title.Text = d.DisplayName + (more > 0 ? $"  +{more} more" : "");
        _subtitle.Text = d.Subtitle;
        _chip.Set(d.StateLabel, d.StateKind);
        _android.Text = d.AndroidLong;
        _build.Text = d.Model.Info.BuildId ?? "—";
        _battery.Text = d.BatteryLong;
        _link.Text = d.Connection;
        _readImei.IsEnabled = d.IsAdbUsable;
    }

    private void ReadImei()
    {
        if (_s.Focused is not { } d) return;
        _readImei.IsEnabled = false;
        _s.Run(Cmd.Of("read_imei", ("serial", d.Serial)), (_, summary, _) =>
        {
            _imei.Text = summary;
            _readImei.IsEnabled = true;
        });
    }
}

// --------------------------------------------------------------------- Apps

public sealed class AppsPage : UserControl
{
    private readonly AppState _s;
    private ApkInfo? _apk;
    private readonly TextBlock _apkTitle = Ui.Text("No APK chosen", 14, bold: true);
    private readonly TextBlock _apkInfo = new() { FontFamily = Palette.Mono, FontSize = 12, Opacity = 0.7 };
    private readonly ToggleSwitch _downgrade = new() { Header = "Allow downgrade", OffContent = "Off", OnContent = "On" };
    private readonly Button _install;
    private readonly TextBox _filter = new() { PlaceholderText = "Filter packages", MinWidth = 260 };
    private readonly CheckBox _system = new() { Content = "System apps" };
    private readonly StackPanel _packages = new() { Spacing = 2 };
    private readonly TextBlock _listTitle = Ui.Text("Installed apps", 16, bold: true);
    private List<Package> _all = [];

    public AppsPage(AppState s, Action<string> go)
    {
        _s = s;
        _install = Ui.Button("Install", () =>
        {
            if (_apk is null) return;
            s.RunVisible(Cmd.Of("install", ("serials", s.Targets), ("path", _apk.Path), ("allow_downgrade", _downgrade.IsOn)));
            go("activity");
        }, accent: true);
        var choose = Ui.Button("Choose…", async () => { if (await Ui.PickFile(".apk") is { } p) SetApk(p); });
        var installCard = Ui.Card(Ui.Column(12,
            Ui.Text("Install", 16, bold: true),
            Ui.Text("Drop an APK anywhere in the window, or choose one.", dim: true),
            Ui.Columns("*", "auto").Put(Ui.Column(2, _apkTitle, _apkInfo).At(0), choose.At(1)),
            _downgrade,
            _install));
        var listCard = Ui.Card(Ui.Column(12,
            _listTitle,
            Ui.Row(12, _filter, _system, Ui.IconButton("\uE72C", "Reload", Reload)),
            _packages));
        Content = Ui.Scroll(Ui.Column(16, installCard, listCard));

        _filter.TextChanged += (_, _) => Render();
        _system.Click += (_, _) => Reload();
        s.TargetsChanged += () => { UpdateInstall(); Reload(); };
        UpdateInstall();
        Reload();
    }

    public void SetApk(string path)
    {
        _apk = _s.InspectApk(path);
        _apkTitle.Text = _apk is null ? "No APK chosen" : _apk.Label ?? _apk.FileName;
        _apkInfo.Text = _apk is null ? "" : $"{_apk.Package}{(_apk.VersionName is { } v ? " " + v : "")} · {Ui.Size(_apk.Size)}";
        UpdateInstall();
    }

    private void UpdateInstall()
    {
        var n = _s.Targets.Count;
        _install.Content = n == 0 ? "Install" : $"Install on {AppState.Count(n, "device", "devices")}";
        _install.IsEnabled = n > 0 && _apk is not null;
    }

    private void Reload()
    {
        var d = _s.Focused;
        if (d is null || !d.IsAdbUsable)
        {
            _listTitle.Text = d is null ? "Select a device to see its apps" : $"{d.DisplayName} is {d.StateLabel.ToLowerInvariant()}";
            _all = [];
            Render();
            return;
        }
        _listTitle.Text = $"Installed apps on {d.DisplayName}";
        _s.Run(Cmd.Of("list_packages", ("serial", d.Serial), ("include_system", _system.IsChecked == true)), (status, _, data) =>
        {
            _all = status == "succeeded" ? Json.As<List<Package>>(data) ?? [] : [];
            Render();
        });
    }

    private void Render()
    {
        _packages.Children.Clear();
        var q = _filter.Text.Trim();
        var shown = _all.Where(p => q.Length == 0 || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(300).ToList();
        if (shown.Count == 0) _packages.Children.Add(Ui.Text(_all.Count == 0 ? "No apps loaded" : "No matching apps", dim: true));
        foreach (var p in shown)
        {
            var name = new TextBlock { Text = p.Name, FontFamily = Palette.Mono, VerticalAlignment = VerticalAlignment.Center };
            var tag = Ui.Text(p.System ? "System" : "", 12, dim: true);
            var del = Ui.IconButton("\uE74D", "Uninstall from targets", async () =>
            {
                var n = _s.Targets.Count;
                if (!await Ui.Confirm(XamlRoot, $"Uninstall {p.Name} from {AppState.Count(n, "device", "devices")}?",
                        "The app and its data are removed.", "Uninstall")) return;
                _s.Run(Cmd.Of("uninstall", ("serials", _s.Targets), ("package", p.Name)), (_, summary, _) => Reload());
            });
            _packages.Children.Add(Ui.Columns("*", "auto", "auto").Put(name.At(0), tag.At(1), del.At(2)));
        }
    }
}

// -------------------------------------------------------------------- Files

public sealed class FilesPage : Grid
{
    private readonly AppState _s;
    private string _path = "/sdcard";
    private readonly TextBox _pathBox = new() { FontFamily = Palette.Mono };
    private readonly StackPanel _entries = new() { Spacing = 2 };
    private readonly TextBlock _status = Ui.Text("", 12, dim: true);
    private readonly TextBlock _device = Ui.Text("", 13, dim: true);

    public FilesPage(AppState s)
    {
        _s = s;
        Padding = new Thickness(24, 0, 24, 24);
        RowSpacing = 10;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _pathBox.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) Open(_pathBox.Text); };
        var bar = Ui.Columns("auto", "*", "auto", "auto", "auto", "auto").Put(
            Ui.IconButton("\uE74A", "Parent folder", () => Open(ParentOf(_path))).At(0),
            _pathBox.At(1),
            Ui.IconButton("\uE72C", "Reload", Reload).At(2),
            Ui.IconButton("\uE8F4", "New folder", NewFolder).At(3),
            Ui.Button("Push file…", async () => { if (await Ui.PickFile() is { } p) Push(p); }).At(4),
            Ui.Button("Push folder…", async () => { if (await Ui.PickFolder() is { } p) Push(p); }).At(5));
        var list = Ui.Card(new ScrollViewer { Content = _entries });
        Grid.SetRow(_device, 1);
        Grid.SetRow(list, 2);
        Grid.SetRow(_status, 3);
        Children.Add(bar);
        Children.Add(_device);
        Children.Add(list);
        Children.Add(_status);
        s.TargetsChanged += Reload;
        Reload();
    }

    private static string Join(string b, string n) => b.EndsWith('/') ? b + n : b + "/" + n;

    private static string ParentOf(string p)
    {
        var t = p.TrimEnd('/');
        var i = t.LastIndexOf('/');
        return i <= 0 ? "/" : t[..i];
    }

    private void Open(string p)
    {
        _path = string.IsNullOrWhiteSpace(p) ? "/" : p.Trim();
        Reload();
    }

    private void Reload()
    {
        _pathBox.Text = _path;
        _entries.Children.Clear();
        var d = _s.Focused;
        if (d is null) { _device.Text = "Select a device to browse its files."; _status.Text = ""; return; }
        if (!d.IsAdbUsable) { _device.Text = $"{d.DisplayName} is {d.StateLabel.ToLowerInvariant()}."; _status.Text = ""; return; }
        var more = _s.Targets.Count - 1;
        _device.Text = $"Browsing {d.DisplayName}" + (more > 0 ? $" · pushes go to all {more + 1} targets" : "");
        _status.Text = "Loading…";
        var path = _path;
        _s.Run(Cmd.Of("list_dir", ("serial", d.Serial), ("path", path)), (status, summary, data) =>
        {
            _status.Text = summary;
            if (status != "succeeded") return;
            var entries = Json.As<List<DirEntry>>(data?["entries"]) ?? [];
            if (entries.Count == 0) _entries.Children.Add(Ui.Text("This folder is empty", dim: true));
            foreach (var e in entries) _entries.Children.Add(EntryRow(d, path, e));
        });
    }

    private Grid EntryRow(DeviceItem d, string path, DirEntry e)
    {
        var full = Join(path, e.Name);
        var glyph = e.Kind switch { "dir" => "\uE8B7", "symlink" => "\uE71B", _ => "\uE8A5" };
        FrameworkElement name = e.Kind == "file"
            ? Ui.Text(e.Name)
            : new HyperlinkButton { Content = e.Name, Padding = new Thickness(0) };
        if (name is HyperlinkButton link) link.Click += (_, _) => Open(full);
        var pull = Ui.IconButton("\uE896", "Pull to this PC", async () =>
        {
            if (await Ui.PickFolder() is { } dir) _s.RunVisible(Cmd.Of("pull", ("serial", d.Serial), ("remote", full), ("local", dir)));
        });
        var del = Ui.IconButton("\uE74D", "Delete", async () =>
        {
            if (!await Ui.Confirm(XamlRoot, $"Delete {e.Name}?", $"It is removed from {d.DisplayName}. This can't be undone.", "Delete")) return;
            _s.Run(Cmd.Of("delete", ("serial", d.Serial), ("path", full)), (_, _, _) => Reload());
        });
        return Ui.Columns("auto", "*", "90", "auto", "auto").Put(
            new FontIcon { Glyph = glyph, FontSize = 16 }.At(0), name.At(1),
            Ui.Text(e.Kind == "file" ? Ui.Size(e.Size) : "", 12, dim: true).At(2), pull.At(3), del.At(4));
    }

    private void Push(string local) =>
        _s.Run(Cmd.Of("push", ("serials", _s.Targets), ("local", local), ("remote", _path.TrimEnd('/') + "/")), (_, _, _) => Reload());

    private async void NewFolder()
    {
        var name = await Ui.Prompt(XamlRoot, "New folder", $"Create a folder in {_path} on every target.", "Folder name", "Create");
        if (name is not null) _s.Run(Cmd.Of("make_dir", ("serials", _s.Targets), ("path", Join(_path, name))), (_, _, _) => Reload());
    }
}

// -------------------------------------------------------------------- Shell

public sealed class ShellPage : Grid
{
    private readonly StackPanel _lines = new() { Spacing = 1, Padding = new Thickness(12) };
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _targets = Ui.Text("", 12, dim: true);
    private readonly Button _run;

    public ShellPage(AppState s)
    {
        Padding = new Thickness(24, 0, 24, 24);
        RowSpacing = 8;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var box = new TextBox { PlaceholderText = "Command, e.g. getprop ro.build.version.release", FontFamily = Palette.Mono };
        void Submit()
        {
            var c = box.Text.Trim();
            if (c.Length > 0 && s.Targets.Count > 0) s.RunShell(c);
        }
        box.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) Submit(); };
        _run = Ui.Button("Run", Submit, accent: true);
        var bar = Ui.Columns("*", "auto", "auto").Put(box.At(0), _run.At(1), Ui.IconButton("\uE894", "Clear output", () => s.Shell.Clear()).At(2));
        _scroll = new ScrollViewer { Content = _lines };
        var output = Ui.Card(_scroll);
        output.Padding = new Thickness(0);
        Grid.SetRow(_targets, 1);
        Grid.SetRow(output, 2);
        Children.Add(bar);
        Children.Add(_targets);
        Children.Add(output);

        void UpdateTargets()
        {
            var n = s.Targets.Count;
            _targets.Text = n == 0 ? "Select one or more devices to run commands on." : $"Runs on {AppState.Count(n, "device", "devices")}";
            _run.IsEnabled = n > 0;
        }
        s.TargetsChanged += UpdateTargets;
        UpdateTargets();
        foreach (var l in s.Shell) Add(l);
        s.Shell.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) _lines.Children.Clear();
            foreach (var l in e.NewItems?.OfType<ShellLine>() ?? []) Add(l);
        };
    }

    private void Add(ShellLine l)
    {
        var tb = new TextBlock
        {
            Text = l.Text,
            FontFamily = Palette.Mono,
            FontSize = 13,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = l.Kind is ShellLineKind.Stdout or ShellLineKind.Stderr ? Microsoft.UI.Text.FontWeights.Normal : Microsoft.UI.Text.FontWeights.SemiBold,
        };
        if (l.Kind == ShellLineKind.Stderr) tb.Foreground = Palette.Chip("error", ActualTheme == ElementTheme.Dark).Fg;
        _lines.Children.Add(tb);
        _scroll.UpdateLayout();
        _scroll.ChangeView(null, _scroll.ScrollableHeight, null, true);
    }
}

// ----------------------------------------------------------------- Routines

public sealed class RoutinesPage : UserControl
{
    private readonly AppState _s;
    private readonly Action<string> _go;
    private Routine? _current;
    private readonly StackPanel _steps = new() { Spacing = 6 };
    private readonly TextBlock _name = Ui.Text("", 16, bold: true);
    private readonly Button _runButton;
    private readonly Border _stepsCard;

    public RoutinesPage(AppState s, Action<string> go)
    {
        _s = s;
        _go = go;
        var list = new StackPanel { Spacing = 6 };
        foreach (var t in s.Templates())
        {
            var b = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = Ui.Column(2, Ui.Text(t.Name, 14, bold: true), Ui.Text(t.Description, 12, dim: true)),
            };
            b.Click += (_, _) => { _current = t; Render(); };
            list.Children.Add(b);
        }
        _runButton = Ui.Button("Run", () =>
        {
            if (_current is not null && s.RunRoutine(_current) is not null) go("activity");
        }, accent: true);
        _stepsCard = Ui.Card(Ui.Column(10, Ui.Columns("*", "auto").Put(_name.At(0), _runButton.At(1)), _steps));
        _stepsCard.Visibility = Visibility.Collapsed;
        Content = Ui.Scroll(Ui.Column(16,
            Ui.Card(Ui.Column(10, Ui.Text("Routines", 16, bold: true),
                Ui.Text("A routine runs its steps in order on every target. Devices run in parallel; a device stops at its first failed step.", dim: true),
                list)),
            _stepsCard));
        s.TargetsChanged += UpdateRun;
    }

    private void UpdateRun()
    {
        var n = _s.Targets.Count;
        _runButton.Content = n == 0 ? "Run" : $"Run on {AppState.Count(n, "device", "devices")}";
        _runButton.IsEnabled = n > 0 && _current is not null;
    }

    private void Render()
    {
        _steps.Children.Clear();
        if (_current is null) { _stepsCard.Visibility = Visibility.Collapsed; return; }
        _stepsCard.Visibility = Visibility.Visible;
        _name.Text = _current.Name;
        for (var i = 0; i < _current.Steps.Count; i++)
        {
            var step = _current.Steps[i];
            var detail = step.Type switch
            {
                "install" => step.Path,
                "fastboot_boot" => step.Image,
                "push" => step.Local,
                "shell" => step.Command,
                _ => null,
            };
            var text = Ui.Column(1, Ui.Text(step.Title),
                new TextBlock { Text = detail is null ? "" : detail.Length == 0 ? "Not chosen yet" : detail, FontFamily = Palette.Mono, FontSize = 12, Opacity = 0.7 });
            FrameworkElement chooser = step.Type switch
            {
                "install" => Ui.Button(string.IsNullOrEmpty(step.Path) ? "Choose APK…" : "Change…", async () => { if (await Ui.PickFile(".apk") is { } p) { step.Path = p; Render(); } }, accent: string.IsNullOrEmpty(step.Path)),
                "fastboot_boot" => Ui.Button(string.IsNullOrEmpty(step.Image) ? "Choose image…" : "Change…", async () => { if (await Ui.PickFile(".img") is { } p) { step.Image = p; Render(); } }, accent: string.IsNullOrEmpty(step.Image)),
                "push" => Ui.Button(string.IsNullOrEmpty(step.Local) ? "Choose folder…" : "Change…", async () => { if (await Ui.PickFolder() is { } p) { step.Local = p; Render(); } }, accent: string.IsNullOrEmpty(step.Local)),
                _ => new Border(),
            };
            _steps.Children.Add(Ui.Columns("28", "*", "auto").Put(Ui.Text($"{i + 1}", 13, bold: true, dim: true).At(0), text.At(1), chooser.At(2)));
        }
        UpdateRun();
    }
}

// ----------------------------------------------------------------- Activity

public sealed class ActivityPage : UserControl
{
    private readonly StackPanel _jobs = new() { Spacing = 8 };
    private readonly TextBlock _log = new() { FontFamily = Palette.Mono, FontSize = 12, IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _empty = Ui.Text("Nothing has run yet", dim: true);

    public ActivityPage(AppState s)
    {
        Content = Ui.Scroll(Ui.Column(16,
            Ui.Column(8, Ui.Text("Jobs", 16, bold: true), _empty, _jobs),
            new Expander { Header = "Log", Content = _log, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch }));
        foreach (var j in s.Jobs.Reverse()) Add(s, j);
        s.Jobs.CollectionChanged += (_, e) => { foreach (var j in e.NewItems?.OfType<JobItem>() ?? []) Add(s, j); };
        _log.Text = string.Join("\n", s.Log.TakeLast(200));
        s.Log.CollectionChanged += (_, _) => _log.Text = string.Join("\n", s.Log.TakeLast(200));
    }

    private void Add(AppState s, JobItem j)
    {
        _empty.Visibility = Visibility.Collapsed;
        var chip = new Chip();
        var bar = new ProgressBar { Maximum = 1 };
        var detail = Ui.Text("", 12, dim: true);
        var cancel = Ui.IconButton("\uE711", "Cancel", () => s.CallNow(Cmd.Of("cancel", ("job_id", j.Id))));
        void Fill()
        {
            chip.Set(j.StatusLabel, j.StatusKind);
            bar.Value = j.Progress;
            bar.IsIndeterminate = j.IsRunning && j.Progress == 0;
            detail.Text = j.Detail;
            cancel.Visibility = j.IsRunning ? Visibility.Visible : Visibility.Collapsed;
        }
        j.PropertyChanged += (_, _) => Fill();
        Fill();
        _jobs.Children.Insert(0, Ui.Card(Ui.Column(8,
            Ui.Columns("*", "auto", "auto").Put(Ui.Text(j.Title, 14, bold: true).At(0), chip.At(1), cancel.At(2)),
            bar, detail)));
    }
}

// ----------------------------------------------------------------- Settings

public sealed class SettingsPage : UserControl
{
    public SettingsPage(AppState s)
    {
        var c = s.Config.Clone();
        var adb = new TextBox { Header = "adb", PlaceholderText = "Automatic", Text = c.AdbPath ?? "", FontFamily = Palette.Mono };
        var fastboot = new TextBox { Header = "fastboot", PlaceholderText = "Automatic", Text = c.FastbootPath ?? "", FontFamily = Palette.Mono };
        var auto = new ToggleSwitch { Header = "Start the adb server automatically", IsOn = c.AutoStartServer };
        var port = new NumberBox { Header = "Port", Value = c.AdbPort, Minimum = 1, Maximum = 65535, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var conc = new NumberBox { Header = "Devices at once", Value = c.MaxConcurrency, Minimum = 1, Maximum = 64, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var using_ = Ui.Text("", 12, dim: true);
        void Describe() => using_.Text = s.Server.State == "running"
            ? $"Using {s.Server.AdbPath ?? "adb"} (protocol {s.Server.Version})."
            : "Leave empty to find adb and fastboot automatically (PATH, ANDROID_HOME, the SDK folder).";
        s.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppState.Server)) Describe(); };
        Describe();
        var save = Ui.Button("Save", () =>
        {
            var n = s.Config.Clone();
            n.AdbPath = adb.Text.Trim().Length == 0 ? null : adb.Text.Trim();
            n.FastbootPath = fastboot.Text.Trim().Length == 0 ? null : fastboot.Text.Trim();
            n.AutoStartServer = auto.IsOn;
            n.AdbPort = (int)port.Value;
            n.MaxConcurrency = (int)conc.Value;
            if (s.SaveConfig(n)) AppState.StoreConfig(n);
        }, accent: true);
        Content = Ui.Scroll(Ui.Column(16,
            Ui.Card(Ui.Column(12, Ui.Text("Android platform-tools", 16, bold: true), using_, adb, fastboot)),
            Ui.Card(Ui.Column(12, Ui.Text("adb server", 16, bold: true), auto, port,
                Ui.Button("Restart adb server", () => s.RunVisible(Cmd.Of("restart_server"))))),
            Ui.Card(Ui.Column(12, Ui.Text("Performance", 16, bold: true), conc)),
            Ui.Row(8, save),
            Ui.Text($"ADB Manager · core {CoreBridge.Version}", 12, dim: true)), 720);
    }
}
