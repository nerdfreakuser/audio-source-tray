# Audio Source

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/nerdfreakuser/audio-source-tray)](https://github.com/nerdfreakuser/audio-source-tray/releases/latest)

A Windows system-tray app that shows **which app and playback device** current audio is coming from.

Hover the equalizer icon in the tray. While something is playing, the icon turns green and a popup lists:

- the application (Chrome, Spotify, Discord, …)
- the window or now-playing title when Windows exposes it
- the output device (headphones, speakers, HDMI, …)

Click the icon to pin the popup until you click elsewhere. Right-click for **Run on startup** (on by default) and **Close**.

## Install

Windows 10/11, 64-bit. The release is self-contained — you do not need to install .NET.

### One-liner

In PowerShell:

```powershell
irm https://raw.githubusercontent.com/nerdfreakuser/audio-source-tray/main/install-from-web.ps1 | iex
```

### From a release zip

1. Download [**AudioSource-win-x64.zip**](https://github.com/nerdfreakuser/audio-source-tray/releases/latest) from the latest release.
2. Extract it.
3. Run `install.ps1`.

That copies the app to `%LOCALAPPDATA%\AudioSourceTray`, starts it, adds **Start Menu** and **Desktop** shortcuts, and registers it to run at logon.

Windows may hide a new tray icon behind the `^` overflow. Open that and drag **Audio Source** onto the visible tray if you want it always shown.

SmartScreen may warn on the first run because the exe is not code-signed. Choose **More info** → **Run anyway**.

### From source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/nerdfreakuser/audio-source-tray.git
cd audio-source-tray
.\install.ps1
```

## Uninstall

- Settings → Apps → **Audio Source** → Uninstall  
- or run `uninstall.ps1` from the repo or from `%LOCALAPPDATA%\AudioSourceTray`

## How it works

Audio Source reads Windows audio sessions (WASAPI) to see which processes are actually outputting sound, and optionally enriches that with System Media Transport Controls for a track title.

Apps that bypass the Windows mixer (some ASIO / exclusive-mode tools) will not appear.

## License

[MIT](LICENSE). Audio uses [NAudio](https://github.com/naudio/NAudio) (MIT).
