# WinUI: a TitleBar fails fast when the window's first layout happens while it's minimised

A plain C# WinUI 3 app for a microsoft-ui-xaml issue.

## Run

Needs the .NET 10 SDK and the Windows App SDK runtime of the version you build against (2.6.4 by default). From this folder:

```
pwsh run.ps1 -Sets titlebar-bare
```

`run.ps1` builds the app, runs it once per control set, and prints `ok` or the exit code. Each run quits by itself after 4 seconds. A minimised run never appears on screen, and `noactivate` runs are placed off-screen. `-Version 2.5.1` builds against another Windows App SDK, and `-SelfContained` bundles it for a machine without that runtime. `matrix.ps1` runs every set in every mode against the builds already in `out\`. `-Show` picks how the first show happens:

| `-Show` | First show |
|---|---|
| `minimized` | `ShowWindow(hwnd, SW_SHOWMINNOACTIVE)` instead of `Window.Activate()` |
| `activate` | `Window.Activate()`, the usual first show (this one comes to the front) |
| `placement` | `SetWindowPlacement` with `showCmd = SW_SHOWMINNOACTIVE` |
| `noactivate` | `ShowWindow(hwnd, SW_SHOWNOACTIVATE)`, normal size, off-screen |
| `hidden` | never shown |
| `hidden-then-minimized` | not shown for the first layout, then `SW_SHOWMINNOACTIVE` once the dispatcher is idle |

The window is built in code in `App.xaml.cs`. `titlebar-bare` is just this:

```csharp
var titleBar = new TitleBar { Title = "Minimised first layout" };
window.ExtendsContentIntoTitleBar = true;
window.SetTitleBar(titleBar);
grid.Children.Add(titleBar);
```

## Steps

1. `pwsh run.ps1 -Sets titlebar-bare`

**Expected:** the window starts minimised and is laid out when it's restored, as a Win32 window can be.
**Actual:** the process fails fast (`0xc000027b`, faulting module `Microsoft.UI.Xaml.dll`) during the first layout tick. `Application.UnhandledException` sees `0x80070002` ("The system cannot find the file specified"), with no app code on the stack.

## Results

These are the same on two machines, on Windows App SDK 2.5.1 and 2.6.4, and whether the SDK is framework-dependent or self-contained. The machines are Windows 11 Pro 26200 (`matrix.ps1`, every mode twice for `minimized`) and an Insider build, 26340 (`minimized`, `placement` and `hidden-then-minimized`, repeated). There were no flaky runs: each set either failed every time or passed every time.

| Control set | `minimized` / `placement` | `activate`, `noactivate`, `hidden`, `hidden-then-minimized` |
|---|---|---|
| `titlebar-bare`: `TitleBar` set as the window's title bar | **fails fast** (every run) | ok |
| `titlebar-button`: the same, plus a `Button` in `RightHeader` | **fails fast** (every run) | ok |
| `titlebar-control`: a `TitleBar` that isn't the window's title bar | ok | ok |
| `extend`: a `Border` as the window's title bar | ok | ok |
| `extend-only`: `ExtendsContentIntoTitleBar` only | ok | ok |
| `navview`, `splitview`, `scroll`, `listview`, `gridview`, `infobar`, `none` | ok | ok |

So the trigger is the `TitleBar` control being the window's title bar while the window's client area is zero-sized for its first layout. Laying out once at the normal size first avoids it (`hidden-then-minimized`). My guess is that `TitleBar`'s drag/caption region setup (`InputNonClientPointerSource`, or reading the `AppWindow` title bar insets) fails on a minimised window that has never been shown at its normal size.

Environment: Windows 11 Pro builds 26200 and 26340, x64, .NET 10, unpackaged.
