using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SmaliPatcherReborn.Core;

namespace SmaliPatcherReborn;

public partial class MainWindow : Window
{
    readonly Adb _adb = new();
    readonly Dictionary<string, ToggleSwitch> _toggles = new();
    List<AdbDevice> _devices = new();
    DeviceStatus? _status;
    DeviceSession? _session;
    bool _busy;
    bool _updatingBox;

    public MainWindow()
    {
        AvaloniaXamlLoader_Load();
        BuildPatchList();
        Wire();
        Opened += async (_, _) => await Startup();
    }

    void AvaloniaXamlLoader_Load() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

    T C<T>(string name) where T : Control => this.FindControl<T>(name)!;

    void BuildPatchList()
    {
        var list = C<StackPanel>("PatchList");
        foreach (var p in PatchInfo.All)
        {
            var t = new ToggleSwitch { IsChecked = p.DefaultOn, OnContent = "", OffContent = "", VerticalAlignment = VerticalAlignment.Center };
            _toggles[p.Id] = t;
            var text = new StackPanel { Spacing = 1 };
            text.Children.Add(new TextBlock { Text = p.Title, FontWeight = Avalonia.Media.FontWeight.SemiBold });
            text.Children.Add(new TextBlock { Text = p.Description, Opacity = 0.65, FontSize = 12, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(text);
            Grid.SetColumn(t, 1);
            row.Children.Add(t);
            list.Children.Add(row);
        }
    }

    void Wire()
    {
        C<Button>("RefreshBtn").Click += async (_, _) => await RefreshDevices();
        C<Button>("ConnectBtn").Click += async (_, _) => await ConnectWifi();
        C<ComboBox>("DeviceBox").SelectionChanged += async (_, _) => { if (!_updatingBox) await LoadStatus(); };
        C<Button>("InstallBtn").Click += async (_, _) => await DoInstall();
        C<Button>("UninstallBtn").Click += async (_, _) => await DoUninstall();
        C<Button>("ToggleBtn").Click += async (_, _) => await DoToggle();
        C<Button>("RebootBtn").Click += async (_, _) => await DoReboot();
        C<Button>("ExportBtn").Click += async (_, _) => await DoExport();
        C<Button>("LogBtn").Click += async (_, _) => await DoShowLog();
        C<Button>("ClearBtn").Click += (_, _) => C<TextBox>("LogBox").Text = "";
        var ver = typeof(MainWindow).Assembly.GetName().Version;
        C<TextBlock>("VersionText").Text = $"Version {ver?.Major}.{ver?.Minor}.{ver?.Build}-dev";
        C<Button>("SiteBtn").Click += (_, _) => About.Open(About.Website);
        C<Button>("RepoBtn").Click += (_, _) => About.Open(About.Repo);
        C<Button>("AuthorBtn").Click += (_, _) => About.Open(About.AuthorUrl);
        C<Button>("OrigBtn").Click += (_, _) => About.Open(About.OriginalUrl);
        C<Button>("ExBtn").Click += (_, _) => About.Open(About.UpdateUrl);
    }

    // ---- helpers ------------------------------------------------------------------------------------

    void Log(string line) => Dispatcher.UIThread.Post(() =>
    {
        var box = C<TextBox>("LogBox");
        box.Text += line.TrimEnd() + "\n";
        box.CaretIndex = box.Text.Length;
    });

    async Task Work(Func<Task> job)
    {
        if (_busy) return;
        _busy = true;
        C<ProgressBar>("Busy").IsVisible = true;
        UpdateButtons();
        try { await job(); }
        catch (Exception ex) { Log("Error: " + ex.Message); }
        finally { _busy = false; C<ProgressBar>("Busy").IsVisible = false; UpdateButtons(); }
    }

    void UpdateButtons()
    {
        var ready = _session != null && !_busy;
        var root = ready && _status is { RootOk: true, Manager: not "" };
        C<Button>("InstallBtn").IsEnabled = root;
        C<Button>("UninstallBtn").IsEnabled = root && _status!.ModuleInstalled;
        C<Button>("ToggleBtn").IsEnabled = root && _status!.ModuleInstalled;
        C<Button>("ToggleBtn").Content = _status is { ModuleDisabled: true } ? "Enable" : "Disable";
        C<Button>("RebootBtn").IsEnabled = ready;
        C<Button>("LogBtn").IsEnabled = root;
        C<Button>("RefreshBtn").IsEnabled = !_busy;
        C<Button>("ConnectBtn").IsEnabled = !_busy;
    }

    async Task<bool> Confirm(string title, string message, string yes = "Continue")
    {
        var tcs = new TaskCompletionSource<bool>();
        var win = new Window
        {
            Title = title, Width = 460, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var ok = new Button { Content = yes, Classes = { "accent" }, MinWidth = 90 };
        var no = new Button { Content = "Cancel", MinWidth = 90 };
        ok.Click += (_, _) => { tcs.TrySetResult(true); win.Close(); };
        no.Click += (_, _) => { tcs.TrySetResult(false); win.Close(); };
        win.Closed += (_, _) => tcs.TrySetResult(false);
        win.Content = new StackPanel
        {
            Margin = new Thickness(22), Spacing = 18,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { no, ok } },
            },
        };
        await win.ShowDialog(this);
        return await tcs.Task;
    }

    // ---- flow ---------------------------------------------------------------------------------------

    async Task Startup()
    {
        await Work(async () =>
        {
            var found = await Task.Run(() => _adb.Locate());
            C<TextBlock>("AdbInfo").Text = found ? $"adb: {_adb.Path}  ({_adb.Source})" : "adb was not found on this computer.";
            if (found) await RefreshDevicesCore();
        });
    }

    async Task RefreshDevices() => await Work(RefreshDevicesCore);

    async Task RefreshDevicesCore()
    {
        var prev = (C<ComboBox>("DeviceBox").SelectedItem as AdbDevice)?.Serial;
        _devices = await Task.Run(() => _adb.Devices());
        _updatingBox = true;
        var box = C<ComboBox>("DeviceBox");
        box.ItemsSource = _devices;
        // Never pick a device for the user when several are connected: reading status runs su on it.
        var readyList = _devices.Where(d => d.Ready).ToList();
        var pick = _devices.FirstOrDefault(d => d.Serial == prev) ?? (readyList.Count == 1 ? readyList[0] : null);
        box.SelectedItem = pick;
        _updatingBox = false;
        Log(_devices.Count == 0 ? "No devices found. Enable USB debugging, or use Wireless debugging and Connect."
            : $"{_devices.Count} device(s) found." + (pick == null && readyList.Count > 1 ? " Choose one in the list: nothing is read from a phone until you pick it." : ""));
        await LoadStatusCore();
    }

    async Task LoadStatus() => await Work(LoadStatusCore);

    async Task LoadStatusCore()
    {
        _status = null; _session = null;
        var dev = C<ComboBox>("DeviceBox").SelectedItem as AdbDevice;
        if (dev == null) { ShowStatus(null, null); return; }
        if (!dev.Ready) { Log($"{dev.Serial} is {dev.State}. Unlock the phone and accept the USB debugging prompt."); ShowStatus(dev, null); return; }
        Log($"Reading {dev.Serial}... (approve the superuser prompt on the phone if it appears)");
        var session = new DeviceSession(_adb, dev.Serial);
        var st = await Task.Run(() => session.ReadStatus());
        _session = session; _status = st;
        ShowStatus(dev, st);
        if (st.Api > 0 && st.ModuleInstalled)
        {
            foreach (var p in PatchInfo.All)
                _toggles[p.Id].IsChecked = st.ConfPatches.Split(',').Contains(p.Id);
        }
    }

    void ShowStatus(AdbDevice? dev, DeviceStatus? s)
    {
        void Set(string n, string v) => C<TextBlock>(n).Text = v;
        if (s == null)
        {
            foreach (var n in new[] { "LDevice", "LAndroid", "LRoot", "LJar", "LModule", "LBuild" }) Set(n, "-");
            if (dev != null) Set("LDevice", dev.ToString());
            UpdateButtons();
            return;
        }
        Set("LDevice", $"{s.Brand} {s.Model}   {s.Serial}".Trim());
        Set("LAndroid", $"Android {s.Release}  (API {s.Api}){(s.SupportedApi ? "" : "  - not supported, needs Android 10+")}");
        Set("LRoot", !s.RootOk ? "No root access. Install Magisk, KernelSU or APatch and grant the shell permission."
                              : $"{(s.Manager == "" ? "root available, but no Magisk/KernelSU/APatch found" : $"{Pretty(s.Manager)} {s.ManagerVersion}")}   (via {(s.RootMode == "adbd" ? "adb root" : "su")})");
        Set("LJar", s.JarSize == 0 ? "services.jar not readable" :
                     s.JarStripped ? $"services.jar is stripped ({s.JarSize:N0} bytes): not supported yet"
                                   : $"services.jar {s.JarSize / 1024 / 1024.0:0.0} MB, patchable");
        Set("LModule", !s.RootOk ? "unknown"
            : !s.ModuleInstalled ? "not installed"
            : $"{s.ModuleVersion}   patches: {(s.ModuleApplied == "" ? "none" : s.ModuleApplied)}"
              + (s.ModuleDisabled ? "   DISABLED" : "") + (s.ModuleRemoving ? "   removal pending" : "")
              + (!s.FingerprintMatches ? "   NEEDS RE-PATCH (ROM changed)" : "")
              + (s.GuardLog != "" ? $"\nSafety: {s.GuardLog}" : ""));
        Set("LBuild", s.Fingerprint);
        UpdateButtons();
    }

    static string Pretty(string m) => m switch { "magisk" => "Magisk", "ksu" => "KernelSU", "apatch" => "APatch", _ => m };

    IEnumerable<string> SelectedPatches() => PatchInfo.All.Where(p => _toggles[p.Id].IsChecked == true).Select(p => p.Id);

    async Task ConnectWifi()
    {
        var text = C<TextBox>("WifiBox").Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        await Work(async () =>
        {
            var r = await Task.Run(() => _adb.Connect(text));
            Log(r.Output.Trim());
            await RefreshDevicesCore();
        });
    }

    async Task DoInstall()
    {
        if (_session == null || _status == null) return;
        var st = _status;
        if (st.JarStripped) { Log("This ROM's services.jar is stripped; not supported yet."); return; }
        var msg = $"Install Smali Patcher Reborn on {st.Brand} {st.Model} (Android {st.Release})?\n\n" +
                  $"Patches: {string.Join(", ", SelectedPatches())}\n\n" +
                  "It patches your phone's framework at install time and takes effect after a reboot. " +
                  "If the phone fails to boot three times, the module disables itself. Make a backup first.";
        if (!await Confirm("Install module", msg, "Install")) return;
        await Work(async () =>
        {
            var ok = await Task.Run(() => _session.Install(st, SelectedPatches().ToList(), Log));
            await LoadStatusCore();
            if (ok && await Confirm("Reboot", "Reboot the phone now to apply the patches?", "Reboot")) { _session.Reboot(); Log("Rebooting..."); }
        });
    }

    async Task DoUninstall()
    {
        if (_session == null || _status == null) return;
        if (!await Confirm("Uninstall", "Remove the module? The original framework is restored at the next reboot.", "Uninstall")) return;
        await Work(async () =>
        {
            var st = _status!;
            var ok = await Task.Run(() => _session.Uninstall(st, Log));
            await LoadStatusCore();
            if (ok && await Confirm("Reboot", "Reboot the phone now?", "Reboot")) { _session.Reboot(); Log("Rebooting..."); }
        });
    }

    async Task DoToggle()
    {
        if (_session == null || _status == null) return;
        await Work(async () =>
        {
            var st = _status!;
            await Task.Run(() => _session.SetDisabled(st, !st.ModuleDisabled, Log));
            await LoadStatusCore();
        });
    }

    async Task DoReboot()
    {
        if (_session == null) return;
        if (!await Confirm("Reboot", "Reboot the phone now?", "Reboot")) return;
        _session.Reboot();
        Log("Rebooting...");
    }

    async Task DoExport()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save module zip", SuggestedFileName = "SmaliPatcherReborn-module.zip", DefaultExtension = "zip",
        });
        if (file == null) return;
        await using var s = await file.OpenWriteAsync();
        var bytes = DeviceSession.ModuleZipBytes();
        await s.WriteAsync(bytes);
        Log($"Saved module ({bytes.Length / 1024} KB). Flash it in Magisk, KernelSU or APatch.");
    }

    async Task DoShowLog()
    {
        if (_session == null) return;
        await Work(async () => Log(await Task.Run(() => _session.ReadPatchLog())));
    }
}
