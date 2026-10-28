// A WinUI window whose first show is minimised, instead of Window.Activate.
//
//   MinimisedFirstLayout.exe [--show activate|minimized|placement|noactivate|hidden]
//                            [--controls titlebar,navview,splitview,scroll,listview,gridview,infobar]
//                            [--exit-after <ms>]
//
// Each run appends a line to run.log next to the exe. A run that fails fast never logs "exit".

using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MinimisedFirstLayout;

public partial class App : Application
{
    readonly string show = "minimized";
    readonly HashSet<string> controls = ["titlebar", "navview", "splitview", "scroll", "listview", "gridview", "infobar"];
    readonly int exitAfter = 4000;
    Window? window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => Log($"unhandled 0x{e.Exception.HResult:x8} {e.Message}\n{e.Exception.StackTrace}");
        var args = Environment.GetCommandLineArgs()[1..];
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--show": show = args[i + 1]; break;
                case "--controls": controls = [.. args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries)]; break;
                case "--exit-after": exitAfter = int.Parse(args[i + 1]); break;
            }
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log($"start show={show} controls={string.Join(',', controls)}");
        Log($"resources {Resources.GetType().Name} themes={Resources.ThemeDictionaries.Count} merged={Resources.MergedDictionaries.Count} has={Resources.ContainsKey("TabViewScrollButtonBackground")}");

        window = new Window { Title = "Minimised first layout" };
        window.Content = BuildContent(window);
        if (window.Content is FrameworkElement root)
        {
            root.Loaded += (_, _) => Log("loaded");
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        Log("content built");
        switch (show)
        {
            case "hidden":
                break;
            case "hidden-then-minimized":
                // The workaround: lay out once while hidden, then minimise.
                window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                    () => ShowWindow(hwnd, SW_SHOWMINNOACTIVE));
                break;
            case "activate":
                window.Activate();
                break;
            case "minimized":
                ShowWindow(hwnd, SW_SHOWMINNOACTIVE);
                break;
            case "placement":
                var placement = new WINDOWPLACEMENT { length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>() };
                GetWindowPlacement(hwnd, ref placement);
                placement.showCmd = SW_SHOWMINNOACTIVE;
                SetWindowPlacement(hwnd, ref placement);
                break;
            case "noactivate":
                // Off-screen, so a test run doesn't cover anything.
                window.AppWindow.Move(new Windows.Graphics.PointInt32(-20000, -20000));
                ShowWindow(hwnd, SW_SHOWNOACTIVATE);
                break;
        }
        Log("shown");

        var timer = window.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(exitAfter);
        timer.Tick += (_, _) =>
        {
            Log("exit");
            Exit();
        };
        timer.Start();
    }

    UIElement BuildContent(Window window)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());

        // The parts of a TitleBar setup on their own: the control without being the window's title
        // bar, and a plain element as the window's title bar.
        if (controls.Contains("titlebar-control"))
        {
            grid.Children.Add(new TitleBar { Title = "Minimised first layout" });
        }
        if (controls.Contains("extend"))
        {
            var bar = new Border { Height = 48, Child = new TextBlock { Text = "Minimised first layout" } };
            window.ExtendsContentIntoTitleBar = true;
            window.SetTitleBar(bar);
            grid.Children.Add(bar);
        }
        if (controls.Contains("extend-only"))
        {
            window.ExtendsContentIntoTitleBar = true;
        }
        // A TitleBar as the window's title bar: bare, then with one interactive element in it.
        if (controls.Contains("titlebar-bare") || controls.Contains("titlebar-button"))
        {
            var titleBar = new TitleBar { Title = "Minimised first layout" };
            if (controls.Contains("titlebar-button"))
            {
                titleBar.RightHeader = new Button { Content = "Right" };
            }
            window.ExtendsContentIntoTitleBar = true;
            window.SetTitleBar(titleBar);
            grid.Children.Add(titleBar);
        }

        if (controls.Contains("titlebar"))
        {
            var titleBar = new TitleBar
            {
                Title = "Minimised first layout",
                IsBackButtonVisible = true,
                IsPaneToggleButtonVisible = true,
                Content = new AutoSuggestBox { PlaceholderText = "Search", MinWidth = 300 },
                RightHeader = new Button { Content = "Right" },
            };
            window.ExtendsContentIntoTitleBar = true;
            window.SetTitleBar(titleBar);
            grid.Children.Add(titleBar);
        }

        if (controls.Contains("infobar"))
        {
            var infoBar = new InfoBar { Title = "InfoBar", Message = "Open at first layout.", IsOpen = true, IsClosable = true };
            Grid.SetRow(infoBar, 1);
            grid.Children.Add(infoBar);
        }

        UIElement body = BuildBody();
        if (controls.Contains("splitview"))
        {
            body = new SplitView
            {
                DisplayMode = SplitViewDisplayMode.Inline,
                PanePlacement = SplitViewPanePlacement.Right,
                IsPaneOpen = true,
                OpenPaneLength = 320,
                Pane = BuildList(20),
                Content = body,
            };
        }
        if (controls.Contains("navview"))
        {
            var nav = new NavigationView { PaneDisplayMode = NavigationViewPaneDisplayMode.Left, IsSettingsVisible = true, Content = body };
            foreach (var name in new[] { "Home", "Albums", "Artists", "Playlists" })
            {
                nav.MenuItems.Add(new NavigationViewItem { Content = name, Icon = new SymbolIcon(Symbol.Audio) });
            }
            nav.SelectedItem = nav.MenuItems[0];
            body = nav;
        }

        Grid.SetRow((FrameworkElement)body, 2);
        grid.Children.Add(body);
        return grid;
    }

    UIElement BuildBody()
    {
        var stack = new StackPanel { Spacing = 8, Padding = new Thickness(16) };
        if (controls.Contains("gridview"))
        {
            var tiles = new GridView { SelectionMode = ListViewSelectionMode.None, IsItemClickEnabled = true, Height = 400 };
            for (var i = 0; i < 40; i++)
            {
                tiles.Items.Add(new Border { Width = 160, Height = 160, Child = new TextBlock { Text = $"Tile {i}" } });
            }
            stack.Children.Add(tiles);
        }
        if (controls.Contains("listview"))
        {
            stack.Children.Add(BuildList(50));
        }
        for (var i = 0; i < 30; i++)
        {
            stack.Children.Add(new TextBlock { Text = $"Line {i}" });
        }
        return controls.Contains("scroll") ? new ScrollViewer { Content = stack } : stack;
    }

    static ListView BuildList(int count)
    {
        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, Height = 300 };
        for (var i = 0; i < count; i++)
        {
            list.Items.Add($"Track {i}");
        }
        return list;
    }

    static void Log(string message)
    {
        var path = Environment.GetEnvironmentVariable("REPRO_LOG") ?? Path.Combine(AppContext.BaseDirectory, "run.log");
        File.AppendAllText(path,
            $"{DateTime.Now:HH:mm:ss.fff} pid={Environment.ProcessId} {message}{Environment.NewLine}");
    }

    const int SW_SHOWNOACTIVATE = 4;
    const uint SW_SHOWMINNOACTIVE = 7;

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct WINDOWPLACEMENT
    {
        public uint length, flags, showCmd;
        public POINT ptMinPosition, ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [DllImport("user32.dll")]
    static extern bool ShowWindow(nint hwnd, int cmd);

    static bool ShowWindow(nint hwnd, uint cmd) => ShowWindow(hwnd, (int)cmd);

    [DllImport("user32.dll")]
    static extern bool GetWindowPlacement(nint hwnd, ref WINDOWPLACEMENT placement);

    [DllImport("user32.dll")]
    static extern bool SetWindowPlacement(nint hwnd, ref WINDOWPLACEMENT placement);
}
