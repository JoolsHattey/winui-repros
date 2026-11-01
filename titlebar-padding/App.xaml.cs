// A window whose title bar is a TitleBar with a red box in RightHeader. Logs the padding column
// WinUI reserves for the caption buttons next to the width they actually take.
//
//   TitleBarPadding.exe [--workaround] [--start <x>,<y>] [--move <x>,<y>] [--exit-after <ms>]
//
// --start        open the window at this screen point (physical pixels)
// --workaround   set RightPaddingColumn to RightInset / RasterizationScale, on load and on every
//                XamlRoot.Changed
// --move         after the first log, move the window to this screen point (physical pixels), to
//                put it on a monitor with a different scale
// --exit-after   quit after this long (default 0: stay open)
//
// Each measurement appends a line to run.log next to the exe.

using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace TitleBarPadding;

public partial class App : Application
{
    readonly bool workaround;
    readonly Windows.Graphics.PointInt32? start;
    readonly Windows.Graphics.PointInt32? move;
    readonly int exitAfter;
    Window? window;
    TitleBar? titleBar;
    Border? box;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => Log($"unhandled 0x{e.Exception.HResult:x8} {e.Message}\n{e.Exception.StackTrace}");
        var args = Environment.GetCommandLineArgs()[1..];
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--workaround": workaround = true; break;
                case "--start": start = Point(args[++i]); break;
                case "--move": move = Point(args[++i]); break;
                case "--exit-after": exitAfter = int.Parse(args[++i]); break;
            }
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new Window { Title = "TitleBar padding" };
        box = new Border { Width = 120, Height = 32, Background = new SolidColorBrush(Microsoft.UI.Colors.Red) };
        titleBar = new TitleBar { Title = "TitleBar padding", RightHeader = box };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.Children.Add(titleBar);
        var hint = new TextBlock
        {
            Margin = new Thickness(16),
            TextWrapping = TextWrapping.Wrap,
            Text = "The red box is the RightHeader content. The gap between it and the minimize button "
                + "should be the same at every display scale.",
        };
        Grid.SetRow(hint, 1);
        grid.Children.Add(hint);

        window.Content = grid;
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(titleBar);
        window.AppWindow.Resize(new(900, 400));
        if (start is { } at)
        {
            window.AppWindow.Move(at);
        }

        titleBar.Loaded += (_, _) =>
        {
            titleBar.XamlRoot.Changed += (_, _) => Measure("XamlRoot.Changed");
            // After WinUI's own UpdatePadding, which runs on template apply.
            window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                Measure("loaded");
                if (move is { } point)
                {
                    window.AppWindow.Move(point);
                    // Once the move's layout has settled, whether or not XamlRoot.Changed fired.
                    var settled = window.DispatcherQueue.CreateTimer();
                    settled.Interval = TimeSpan.FromMilliseconds(500);
                    settled.IsRepeating = false;
                    settled.Tick += (_, _) => Measure("after the move");
                    settled.Start();
                }
            });
        };
        window.Activate();

        if (exitAfter > 0)
        {
            var timer = window.DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(exitAfter);
            timer.Tick += (_, _) => Exit();
            timer.Start();
        }
    }

    void Measure(string when)
    {
        if (window is null || titleBar is null || box is null || titleBar.XamlRoot is null
            || VisualTreeHelper.GetChildrenCount(titleBar) == 0
            || VisualTreeHelper.GetChild(titleBar, 0) is not FrameworkElement root
            || root.FindName("RightPaddingColumn") is not ColumnDefinition column)
        {
            return;
        }
        var scale = titleBar.XamlRoot.RasterizationScale;
        var inset = window.AppWindow.TitleBar.RightInset;
        var buttons = inset / scale;
        if (workaround && buttons > 0)
        {
            column.Width = new GridLength(buttons);
            titleBar.UpdateLayout();
        }
        var boxRight = box.TransformToVisual(null).TransformPoint(new(box.ActualWidth, 0)).X;
        var gap = titleBar.XamlRoot.Size.Width - buttons - boxRight;
        Log($"{when}: dpi={GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window))} "
            + $"RasterizationScale={scale} RightInset={inset}px "
            + $"caption buttons={buttons:0.#} DIPs RightPaddingColumn={column.ActualWidth:0.#} DIPs "
            + $"gap before the buttons={gap:0.#} DIPs{(workaround ? " (workaround on)" : "")}");
    }

    static Windows.Graphics.PointInt32 Point(string xy)
    {
        var parts = xy.Split(',');
        return new(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    static void Log(string message)
    {
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "run.log"),
            $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
    }

    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(nint hwnd);
}
