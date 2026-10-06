using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SmaliPatcherReborn.Core;

namespace SmaliPatcherReborn;

public enum DonateResult { Later, Never }   // Later is the default, so closing the window with X means Later

/// <summary>Compact wallet list with Copy buttons. After a patch it also offers Later / Never; opened from the Donate button it only closes.</summary>
public sealed class DonateDialog : Window
{
    const string Heart = "M12,21.35 L10.55,20.03 C5.4,15.36 2,12.28 2,8.5 C2,5.42 4.42,3 7.5,3 C9.24,3 10.91,3.81 12,5.09 C13.09,3.81 14.76,3 16.5,3 C19.58,3 22,5.42 22,8.5 C22,12.28 18.6,15.36 13.45,20.03 Z";

    readonly bool _afterPatch;
    readonly List<Button> _copyButtons = new();

    public DonateDialog(bool afterPatch)
    {
        _afterPatch = afterPatch;
        Title = "Support Smali Patcher Reborn";
        Width = 520; SizeToContent = SizeToContent.Height; CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (Application.Current?.TryFindResource("WindowBg", Application.Current.ActualThemeVariant, out var bg) == true) Background = bg as IBrush;

        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 18), Spacing = 10 };
        body.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 13, Opacity = 0.85,
            Text = "Your support keeps this project free, maintained for every new Android release, and inspires the next free tool. Any amount helps.",
        });
        for (int i = 0; i < Donate.Wallets.Count; i++) body.Children.Add(WalletRow(Donate.Wallets[i], i));

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 6, 0, 0) };
        footer.Children.Add(new TextBlock
        {
            Classes = { "dim" }, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0),
            Text = "Send only the coin shown, on the network shown.",
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (afterPatch)
        {
            var never = new Button { Content = "Never ask again", Classes = { "link" } };
            var later = new Button { Content = "Later", MinWidth = 88, Classes = { "accent" } };
            never.Click += (_, _) => ClickNever();
            later.Click += (_, _) => ClickLater();
            buttons.Children.Add(never);
            buttons.Children.Add(later);
        }
        else
        {
            var close = new Button { Content = "Close", MinWidth = 88, Classes = { "accent" } };
            close.Click += (_, _) => ClickLater();
            buttons.Children.Add(close);
        }
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);
        body.Children.Add(footer);

        var root = new StackPanel();
        root.Children.Add(Header(afterPatch));
        root.Children.Add(body);
        Content = root;
        Opened += (_, _) => TestHook();
    }

    static Control Header(bool afterPatch)
    {
        var badge = new Border
        {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(23), Background = new SolidColorBrush(Color.Parse("#FFAB00")),
            Child = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(Heart), Fill = new SolidColorBrush(Color.Parse("#004D40")), Width = 24, Height = 24, Stretch = Stretch.Uniform },
        };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2, Margin = new Thickness(14, 0, 0, 0) };
        text.Children.Add(new TextBlock { Text = afterPatch ? "Patch complete. Thank you!" : "Keep free tools free", FontSize = 19, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
        text.Children.Add(new TextBlock { Text = afterPatch ? "Made by one developer, free for everyone." : "Support Smali Patcher Reborn", FontSize = 12.5, Foreground = new SolidColorBrush(Color.Parse("#CCFFFFFF")) });
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(badge);
        row.Children.Add(text);
        return new Border
        {
            Padding = new Thickness(20, 18),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#00897B"), 0), new GradientStop(Color.Parse("#004D40"), 1) },
            },
            Child = row,
        };
    }

    static string CoinColor(string ticker) => ticker switch { "BTC" => "#F7931A", "ETH" => "#627EEA", "USDT" => "#26A17B", _ => "#00796B" };

    Control WalletRow(Wallet w, int index)
    {
        var coin = new Border
        {
            Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Background = new SolidColorBrush(Color.Parse(CoinColor(w.Ticker))),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = w.Ticker, Foreground = Brushes.White, FontSize = w.Ticker.Length > 3 ? 9.5 : 11, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 10, 0) };
        info.Children.Add(new TextBlock { Text = $"{w.Coin}  ·  {w.Network}", FontSize = 12.5, FontWeight = FontWeight.SemiBold });
        info.Children.Add(new SelectableTextBlock
        {
            Text = w.Address, FontSize = 11, Opacity = 0.8, TextWrapping = TextWrapping.Wrap,   // wraps rather than cutting an address off
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace"),
        });
        var copy = new Button { Content = "Copy", MinWidth = 68, Padding = new Thickness(10, 5), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        copy.Click += async (_, _) => await ClickCopy(index);
        _copyButtons.Add(copy);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(info, 1); Grid.SetColumn(copy, 2);
        grid.Children.Add(coin); grid.Children.Add(info); grid.Children.Add(copy);
        return new Border { Classes = { "card" }, Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(12), Child = grid };
    }

    async Task ClickCopy(int index)
    {
        var button = _copyButtons[index];
        try
        {
            var clip = GetTopLevel(this)?.Clipboard;
            if (clip == null) throw new InvalidOperationException("no clipboard");
            await clip.SetTextAsync(Donate.Wallets[index].Address);
            button.Content = "Copied";
        }
        catch { button.Content = "Select it"; }   // the address is selectable, so Ctrl+C still works
        DispatcherTimer.RunOnce(() => button.Content = "Copy", TimeSpan.FromSeconds(2));
    }

    void ClickLater() { Log("result:later"); Close(DonateResult.Later); }
    void ClickNever() { Log("result:never"); Close(DonateResult.Never); }

    // ---- test hook: SPR_DONATE_TEST="copy0,shot,later" runs those steps through the same handlers the buttons use ----
    static void Log(string line)
    {
        var path = Environment.GetEnvironmentVariable("SPR_DONATE_TEST_LOG");
        if (!string.IsNullOrEmpty(path)) try { File.AppendAllText(path, line + "\n"); } catch { }
    }

    void TestHook()
    {
        var script = Environment.GetEnvironmentVariable("SPR_DONATE_TEST");
        if (string.IsNullOrEmpty(script)) return;
        Log("shown:" + (_afterPatch ? "afterpatch" : "manual") + " wallets=" + Donate.Wallets.Count);
        var steps = new Queue<string>(script.Split(','));
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        timer.Tick += async (_, _) =>
        {
            if (steps.Count == 0) { timer.Stop(); return; }
            var s = steps.Dequeue();
            if (s.StartsWith("copy") && int.TryParse(s[4..], out var i))
            {
                await ClickCopy(i);
                var got = GetTopLevel(this)?.Clipboard is { } c ? await c.GetTextAsync() : null;
                Log($"clip{i}={got}");
            }
            else if (s == "shot")
            {
                var path = Environment.GetEnvironmentVariable("SPR_DONATE_SHOT");
                if (!string.IsNullOrEmpty(path))
                {
                    var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
                    using var bmp = new RenderTargetBitmap(size, new Vector(96, 96));
                    bmp.Render(this);
                    bmp.Save(path);
                    Log($"shot:{path} {size.Width}x{size.Height}");
                }
            }
            else if (s == "later") { timer.Stop(); ClickLater(); }
            else if (s == "never") { timer.Stop(); ClickNever(); }
        };
        timer.Start();
    }
}
