# WinUI: TitleBar's caption-button padding isn't scaled for the display (#10344)

A plain C# WinUI 3 app for [microsoft-ui-xaml#10344](https://github.com/microsoft/microsoft-ui-xaml/issues/10344).

## Run

Needs the .NET 10 SDK and the Windows App SDK 2.6.4 runtime. Set the display scale to 150%, then from this folder:

```
dotnet build -c Release
bin\Release\net10.0-windows10.0.26100.0\win-x64\TitleBarPadding.exe
```

The window's title bar is a `TitleBar` with a red box as its `RightHeader`. Each measurement appends a line to `run.log` next to the exe. Options:

| Option | |
|---|---|
| `--workaround` | sets the template's `RightPaddingColumn` to `RightInset / RasterizationScale`, on load and on every `XamlRoot.Changed` |
| `--move <x>,<y>` | after the first measurement, moves the window to that screen point (physical pixels), to put it on a monitor with a different scale |
| `--exit-after <ms>` | quits after this long (default: stays open) |

The app has the WinUI template's `app.manifest` (per-monitor DPI aware). Without it Windows stretches a 96 DPI window and the bug doesn't show.

## Steps

1. Set the display scale to 150%.
2. Run `TitleBarPadding.exe`.

**Expected:** the gap between the red box and the minimize button is the same as at 100%, and `RightPaddingColumn` is as wide as the caption buttons (`AppWindow.TitleBar.RightInset / XamlRoot.RasterizationScale`).
**Actual:** `RightPaddingColumn` is `RightInset` read as DIPs, 1.5 times the caption buttons' width, so the gap grows by half the buttons' width.

## Results

Windows 11 Pro 26200, x64, Windows App SDK 2.6.4, one display:

| Scale | Run | `RightInset` | Caption buttons | `RightPaddingColumn` | Gap before the buttons |
|---|---|---|---|---|---|
| 100% (96 DPI)¹ | default | 137 px | 137 DIPs | 137 DIPs | 48 DIPs |
| 150% | default | 207 px | 138 DIPs | **207.3 DIPs** | **117.3 DIPs** |
| 150% | `--workaround` | 207 px | 138 DIPs | 138 DIPs | 48 DIPs |

¹ The same machine at 150%, with the app built without its manifest, so Windows ran it at 96 DPI (`RasterizationScale` 1).

`screenshot-150.png` is the default run at 150%, and `screenshot-150-workaround.png` is the same with `--workaround`.

`TitleBar::UpdatePadding` sets the padding columns from `AppWindowTitleBar.RightInset` and `LeftInset`, which are physical pixels, without dividing by `RasterizationScale`. It also only runs on template apply and on a flow direction change, so a fix needs to run again on `XamlRoot.Changed` as well, or the width goes stale when the window moves to a monitor with another scale or the display scale changes while the app runs (`--move` is for checking that by hand; it hasn't been run here, since the test machine has one display).
