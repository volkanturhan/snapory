# snapory

**English | [Türkçe](README.tr.md)**

A lightweight Windows screenshot and annotation tool.

snapory lives quietly in your system tray. Press a hotkey, the screen freezes and
dims, you drag out the area you want — then it opens in a little editor where you
can add arrows, boxes, highlights, and text before copying it to the clipboard or
saving it as a PNG.

<p align="center">
  <img src="docs/screenshot-dark.png" alt="snapory — editing canvas and history in one window (dark)" width="400" />
  <img src="docs/screenshot-light.png" alt="snapory — editing canvas and history in one window (light)" width="400" />
</p>

## Features

- **Capture a region** — global hotkey (`Ctrl + Shift + S`) dims the screen and
  lets you drag out exactly the area you want.
- **Pixel-accurate** — captures from a frozen snapshot of the desktop, correct
  even on high-DPI and multi-monitor setups.
- **Annotate** — arrow, box, highlight, and text tools, in a choice of colours.
- **Undo** — step back through your annotations (`Ctrl + Z`).
- **Copy or save** — copy the result to the clipboard (`Ctrl + C`) or save it as
  a PNG (`Ctrl + S`), flattened at full resolution.
- **One window** — the editing canvas and your screenshot history live together:
  shots stack down the right, the selected one opens on the left to edit. Delete
  one or clear them all; the history persists across restarts.
- **Dark or light** — pick a **System**, **Dark**, or **Light** theme from the
  menu. Defaults to **System**, following your Windows setting.
- **Start with Windows** — optional, toggled from the menu.
- **Self-updating** — when a new version ships, snapory offers it from the tray; one click installs it.
- **English & Turkish** — switch the interface language from the menu.
- **Private by design** — everything stays on your machine; nothing is uploaded.

## Download

Grab the latest build from the [**Releases**](https://github.com/volkanturhan/snapory/releases/latest) page:

- **snapory-setup-…exe** — installer (recommended). No admin rights needed, and snapory keeps itself up to date from here on.
- **snapory-…exe** — portable single file; just run it, nothing to install.

Both are self-contained, so you don't need .NET installed. Windows 10/11, 64-bit.

## Run from source

Prefer to build it yourself? You'll need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
(the SDK, not just the runtime) on Windows.

```bash
git clone https://github.com/volkanturhan/snapory.git
cd snapory
dotnet run --project snapory/snapory.csproj
```

snapory starts quietly in the system tray — **no window pops up**. That's normal;
press the hotkey (or use **New screenshot** from the tray) to capture.

## How to use

1. Launch snapory — it starts quietly in the system tray.
2. Press **`Ctrl + Shift + S`** (or pick **New screenshot** from the tray). The
   screen dims; **drag** to select the area you want. **Esc** cancels.
3. The shot opens in snapory's window — the canvas on the left, your history down
   the right. Pick a tool (**Arrow**, **Box**, **Highlight**, **Text**) and a
   colour, then draw on it. **Undo** / **Ctrl + Z** removes the last mark.
4. **Copy** (`Ctrl + C`) puts the result on your clipboard; **Save** (`Ctrl + S`)
   writes a PNG. Both update the shot in your history.
5. Click any thumbnail on the right to open that shot for editing; **Delete**
   removes the selected one and **Clear all** empties the history.

Right-click the tray icon for **New screenshot**, **Open** (the window), **Start
with Windows**, language, and **Quit**; double-clicking the tray icon opens the
window too.

## Build it yourself

Want to produce the release artifacts locally? They aren't checked into the repo:

```bash
# Portable self-contained exe + the Windows installer, into dist/release.
# (The installer step needs Inno Setup: winget install JRSoftware.InnoSetup)
pwsh tools/release.ps1
```

## Tech

- C# / WPF on .NET 8 (Windows)
- No third-party dependencies

## License

MIT — see [LICENSE](LICENSE).
