# Menus opened at the pointer show the wait cursor or a resize cursor

For [microsoft/microsoft-ui-xaml#8829](https://github.com/microsoft/microsoft-ui-xaml/issues/8829).

A `MenuFlyout` opened at or next to the pointer (a `ContextFlyout`, a `Button.Flyout` opened by a click near the button's bottom edge, a `ComboBox` drop-down) shows the wait cursor for a moment, or a resize cursor that stays until the pointer moves.

## Run

```
dotnet build -c Release
bin\Release\...\MenuCursor.exe          # click the targets yourself
bin\Release\...\MenuCursor.exe --auto   # moves the real pointer and clicks each case, then exits
```

A background thread samples the system cursor (`GetCursorInfo`) every 2 ms and logs every change of cursor or of the window under the pointer, in the window and in `cursor.log` next to the exe. When a menu opens it also logs the visible menu's size and the size of the window under the pointer. Don't touch the mouse while `--auto` runs.

## Cases

1. `Button.Flyout` menu, clicked 3 px above the button's bottom edge, so the menu opens just below the pointer.
2. The same button clicked in the middle.
3. A `ContextFlyout` menu, right-clicked.
4. A `ComboBox`, clicked.
5. A button whose menu opens with `ShowAt(button, FlyoutShowOptions { ExclusionRect = button bounds })`.
6. A right-click target that handles `ContextRequested` and opens with `FlyoutShowOptions { Position = pointer, ExclusionRect = around the pointer }`.

Cases 5 and 6 are the workaround.

## Cause, as far as I can tell

A `MenuFlyout` always gets its own popup window, and that window is larger than the visible menu by its shadow (about 2 DIPs above, 10 beside and 18 below). When the menu opens at or next to the pointer, the pointer ends up over that popup window, often over its transparent shadow margin. Until the popup's input site is ready the system shows the wait cursor, and at the window's edge it shows the resize cursor for that edge, which stays until the pointer moves.

Keeping the popup window away from the pointer avoids it: `FlyoutShowOptions` with an `ExclusionRect` around the pointer or the button, which WinUI honours whichever way the menu opens or flips.

## Results

Windows App SDK 2.6.4, `--auto`, 2026-10-30.

Desktop PC (Windows 11 build 26340, 100% scale), cursor sampled every 2 ms:

| Case | Window under the pointer after opening | Cursor |
|---|---|---|
| 1. `Button.Flyout`, click near the bottom edge | the menu's popup window | wait, about 16 ms |
| 2. `Button.Flyout`, click in the middle | the app window | arrow |
| 3. `ContextFlyout`, right-click | the menu's popup window | wait, about 15 ms |
| 4. `ComboBox`, click | the drop-down's popup window | wait, still showing 2 s later |
| 5. `FlyoutShowOptions` button | the app window | arrow |

The visible menu was 117x102 px and its popup window 137x122 px.

Test laptop (Windows 11 build 26200), all six cases: the pointer ended up over the popup window in cases 1, 3 and 4 only. With the clearances above, cases 5 and 6 leave the pointer over the app window. The laptop has no mouse attached, so its cursor is hidden and only the window under the pointer was checked there; case 6 with these clearances hasn't been checked with a visible cursor.
