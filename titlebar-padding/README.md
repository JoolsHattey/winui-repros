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

3. With a second monitor at 100% to the right of a 3840-pixel-wide primary at 150%, run `TitleBarPadding.exe --start 200,200 --move 4200,200`, then `--start 4200,200 --move 200,200`.

**Expected:** `RightPaddingColumn` follows the scale of the monitor the window is on.
**Actual:** it keeps the width from the monitor the window opened on.

## Results

Windows 11 Pro 26200 and Insider 26340, x64, Windows App SDK 2.6.4. Opened on one monitor:

| Scale | Run | `RightInset` | Caption buttons | `RightPaddingColumn` | Gap before the buttons |
|---|---|---|---|---|---|
| 100% | default | 138 px | 138 DIPs | 138 DIPs | 48 DIPs |
| 150% | default | 207 px | 138 DIPs | **207.3 DIPs** | **117.3 DIPs** |
| 150% | `--workaround` | 207 px | 138 DIPs | 138 DIPs | 48 DIPs |

Moved to another monitor (26340, primary at 150%, second monitor at 100%), measured 500 ms after the move:

| Run | Opened at | Moved to | `RightPaddingColumn` after the move | Gap before the buttons |
|---|---|---|---|---|
| default | 150% | 100% | **207 DIPs** (should be 138) | **117 DIPs** |
| default | 100% | 150% | 138 DIPs (right only because it opened at 100%) | 48 DIPs |
| `--workaround` | 150% | 100% | 138 DIPs | 48 DIPs |
| `--workaround` | 100% | 150% | 138 DIPs | 48 DIPs |

`screenshot-150.png` is the default run at 150%, and `screenshot-150-workaround.png` is the same with `--workaround`.

`TitleBar::UpdatePadding` sets the padding columns from `AppWindowTitleBar.RightInset` and `LeftInset`, which are physical pixels, without dividing by `RasterizationScale`. It also only runs on template apply and on a flow direction change, so the width never follows the window to a monitor with another scale. A fix needs to run again on `XamlRoot.Changed` as well.
