// Menus that open at or next to the pointer show the wait cursor or a resize cursor.
//
//   MenuCursor.exe [--auto]
//
// The window has one target per case. A background thread samples the system cursor every 2 ms
// and logs each change, with the window under the pointer, to the window and to cursor.log next
// to the exe. --auto moves the real pointer and clicks each case in turn (don't touch the mouse
// while it runs), then exits.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace MenuCursor;

public partial class App : Application
{
    readonly bool auto = Environment.GetCommandLineArgs().Contains("--auto");
    readonly string logPath = Path.Combine(AppContext.BaseDirectory, "cursor.log");
    readonly ConcurrentQueue<string> pending = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    Window? window;
    TextBox? log;
    IntPtr hwnd;

    public App()
    {
        InitializeComponent();
        File.WriteAllText(logPath, "");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new Window { Title = "Menu cursor" };
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 700));

        var buttonMenu = new Button { Content = "Button.Flyout menu", Flyout = Menu() };
        var contextMenu = Target("Right-click: ContextFlyout menu");
        contextMenu.ContextFlyout = Menu();
        var combo = new ComboBox { Header = "ComboBox", ItemsSource = new[] { "One", "Two", "Three" }, SelectedIndex = 0 };

        // The workaround: open with a position and an exclusion rect, so the menu's popup window
        // is never under the pointer.
        var fixedButton = new Button { Content = "Button menu with FlyoutShowOptions" };
        var fixedButtonMenu = Menu();
        fixedButton.Click += (_, _) => fixedButtonMenu.ShowAt(fixedButton, new FlyoutShowOptions
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            ExclusionRect = new Rect(0, 0, fixedButton.ActualWidth, fixedButton.ActualHeight),
        });
        var fixedContext = Target("Right-click: ContextRequested + FlyoutShowOptions");
        var fixedContextMenu = Menu();
        fixedContext.ContextRequested += (_, e) =>
        {
            e.Handled = true;
            var options = new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
            if (e.TryGetPosition(fixedContext, out var point))
            {
                options.Position = point;
                options.ExclusionRect = new Rect(point.X - 1, point.Y - 1, 2, 2);
            }
            fixedContextMenu.ShowAt(fixedContext, options);
        };

        log = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
        };
        ScrollViewer.SetVerticalScrollBarVisibility(log, ScrollBarVisibility.Auto);
        var cases = new StackPanel { Spacing = 16, Children = { buttonMenu, contextMenu, combo, fixedButton, fixedContext } };
        var root = new Grid { Padding = new Thickness(24), ColumnSpacing = 24 };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.Children.Add(cases);
        Grid.SetColumn(log, 1);
        root.Children.Add(log);
        window.Content = root;

        var flush = window.DispatcherQueue.CreateTimer();
        flush.Interval = TimeSpan.FromMilliseconds(100);
        flush.Tick += (_, _) => Flush();
        flush.Start();
        new Thread(SampleCursor) { IsBackground = true }.Start();

        window.Activate();
        Log($"Windows App SDK runtime {Microsoft.Windows.ApplicationModel.WindowsAppRuntime.RuntimeInfo.AsString}, OS {Environment.OSVersion.Version}, scale {GetDpiForWindow(hwnd) / 96.0:0.##}");
        if (auto)
        {
            root.Loaded += async (_, _) => await RunCases(buttonMenu, contextMenu, combo, fixedButton, fixedContext);
        }
    }

    MenuFlyout Menu()
    {
        var menu = new MenuFlyout();
        foreach (var text in new[] { "Play next", "Add to queue", "Go to album" })
        {
            menu.Items.Add(new MenuFlyoutItem { Text = text });
        }
        menu.Opened += (_, _) => Log($"menu opened; {DescribePopup(menu)}");
        menu.Closed += (_, _) => Log("menu closed");
        return menu;
    }

    static Border Target(string text) => new()
    {
        Width = 320,
        Height = 64,
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
        Child = new TextBlock { Text = text, Margin = new Thickness(12), VerticalAlignment = VerticalAlignment.Center },
    };

    // The visible menu's size against the size of the popup window it lives in.
    string DescribePopup(MenuFlyout menu)
    {
        var popup = VisualTreeHelper.GetOpenPopupsForXamlRoot(window!.Content.XamlRoot)
            .FirstOrDefault(p => p.Child is MenuFlyoutPresenter);
        if (popup?.Child is not FrameworkElement presenter)
        {
            return "no popup";
        }
        var scale = window.Content.XamlRoot.RasterizationScale;
        GetCursorPos(out var pointer);
        var under = WindowFromPoint(pointer);
        GetWindowRect(under, out var rect);
        return $"visible menu {presenter.ActualWidth * scale:0}x{presenter.ActualHeight * scale:0} px; " +
            $"window under pointer {Describe(under)} {rect.Right - rect.Left}x{rect.Bottom - rect.Top} px at ({rect.Left},{rect.Top})";
    }

    async Task RunCases(Button buttonMenu, Border contextMenu, ComboBox combo, Button fixedButton, Border fixedContext)
    {
        await Task.Delay(1000);
        // Pointer on the button 3 px above its bottom edge, where the menu opens below it.
        await Case("Button.Flyout, click near the bottom edge", buttonMenu, 0.5, -3, left: true);
        await Case("Button.Flyout, click in the middle", buttonMenu, 0.5, 0.5, left: true);
        await Case("ContextFlyout, right-click", contextMenu, 0.3, 0.5, left: false);
        await Case("ComboBox, click", combo, 0.5, -10, left: true);
        await Case("FlyoutShowOptions button, click near the bottom edge", fixedButton, 0.5, -3, left: true);
        await Case("ContextRequested + FlyoutShowOptions, right-click", fixedContext, 0.3, 0.5, left: false);
        Log("done");
        Flush();
        Exit();
    }

    // x and y are fractions of the element's size, or pixels from its far edge when negative.
    async Task Case(string name, FrameworkElement target, double x, double y, bool left)
    {
        var scale = target.XamlRoot.RasterizationScale;
        var origin = target.TransformToVisual(null).TransformPoint(new Point(0, 0));
        var dx = x < 0 ? target.ActualWidth + x : target.ActualWidth * x;
        var dy = y < 0 ? target.ActualHeight + y : target.ActualHeight * y;
        var client = new POINT { X = (int)((origin.X + dx) * scale), Y = (int)((origin.Y + dy) * scale) };
        ClientToScreen(hwnd, ref client);

        SetCursorPos(client.X, client.Y);
        await Task.Delay(400);
        Log($"--- {name}: pointer at ({client.X},{client.Y}), cursor {CursorName(CurrentCursor())}");
        caseStart = clock.ElapsedMilliseconds;
        Click(left);
        await Task.Delay(2000);
        Log($"after 2 s: cursor {CursorName(CurrentCursor())}");
        SendKey(0x1B);
        await Task.Delay(600);
    }

    long caseStart;

    void SampleCursor()
    {
        var last = IntPtr.Zero;
        var lastWindow = IntPtr.Zero;
        while (true)
        {
            var cursor = CurrentCursor();
            GetCursorPos(out var pointer);
            var under = WindowFromPoint(pointer);
            if (cursor != last || under != lastWindow)
            {
                var at = clock.ElapsedMilliseconds - caseStart;
                pending.Enqueue($"  +{at,5} ms  cursor {CursorName(cursor),-11} over {Describe(under)}");
                last = cursor;
                lastWindow = under;
            }
            Thread.Sleep(2);
        }
    }

    void Log(string line) => pending.Enqueue(line);

    void Flush()
    {
        var text = new StringBuilder();
        while (pending.TryDequeue(out var line))
        {
            text.AppendLine(line);
        }
        if (text.Length == 0)
        {
            return;
        }
        File.AppendAllText(logPath, text.ToString());
        log!.Text += text.ToString();
        log.SelectionStart = log.Text.Length;
    }

    static string Describe(IntPtr window)
    {
        var name = new StringBuilder(256);
        GetClassName(window, name, name.Capacity);
        return $"{name} 0x{window:x}";
    }

    static IntPtr CurrentCursor()
    {
        var info = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        return GetCursorInfo(ref info) ? info.hCursor : IntPtr.Zero;
    }

    static readonly (int Id, string Name)[] Cursors =
    [
        (32512, "arrow"), (32514, "wait"), (32650, "appstarting"), (32513, "ibeam"), (32649, "hand"),
        (32644, "size-we"), (32645, "size-ns"), (32642, "size-nwse"), (32643, "size-nesw"), (32646, "size-all"),
    ];

    static string CursorName(IntPtr cursor)
    {
        foreach (var (id, name) in Cursors)
        {
            if (LoadCursor(IntPtr.Zero, id) == cursor)
            {
                return name;
            }
        }
        return cursor == IntPtr.Zero ? "hidden" : $"0x{cursor:x}";
    }

    static void Click(bool left)
    {
        uint down = left ? 0x0002u : 0x0008u, up = left ? 0x0004u : 0x0010u;
        var inputs = new[] { MouseInput(down), MouseInput(up) };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    static void SendKey(ushort key)
    {
        var inputs = new[]
        {
            new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = key } } },
            new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = key, dwFlags = 0x0002 } } },
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    static INPUT MouseInput(uint flags) => new() { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags } } };

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct CURSORINFO { public int cbSize, flags; public IntPtr hCursor; public POINT ptScreenPos; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion u; }

    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSORINFO info);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern IntPtr LoadCursor(IntPtr instance, int id);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window, ref POINT point);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int count);
}
