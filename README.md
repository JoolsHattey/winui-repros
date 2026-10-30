# WinUI repros

Small, self-contained repros for microsoft-ui-xaml issues. Each folder is one plain C# WinUI 3 app (unpackaged, .NET 10, no other dependencies), with a README giving the steps, the expected and actual result, and what's known about the cause.

| Folder | Issue | Checked on |
|---|---|---|
| [`minimised-first-layout`](minimised-first-layout) | A `TitleBar` set as the window's title bar fails fast when the window's first layout happens while it's minimised | Windows App SDK 2.5.1 and 2.6.4: reproduces on every run |
| [`menu-cursor`](menu-cursor) | Menus opened at or next to the pointer show the wait cursor or a resize cursor (#8829) | Windows App SDK 2.6.4: reproduces (wait cursor on a button menu, a context menu and a ComboBox) |

Repros for `windows-reactor` (Rust) issues are in [windows-reactor-repros](https://github.com/JoolsHattey/windows-reactor-repros).
