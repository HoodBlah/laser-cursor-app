<img width="800" height="450" alt="lasercursor gif" src="https://github.com/user-attachments/assets/94bf10e0-14cc-42ff-b4aa-800b2e801468" />

[Laser Cursor Demo Video/Tutorial](https://www.youtube.com/watch?v=BLM1S0KrkrY)

# Laser Cursor

A lightweight Windows desktop overlay that replaces your boring cursor with a smooth, customizable laser trail. Built for presenters, streamers, and anyone who wants their cursor to actually be visible.

---

## Download

**[→ Latest Release](https://github.com/HoodBlah/laser-cursor-app/releases/latest)**

No install needed — just download and run `LaserCursorApp.exe`.

---

## Support
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/C4Z8209YYL)

---

## Features

### Core
- Smooth Catmull-Rom laser trail that follows the cursor in real time
- Dynamic tapered trail — thick at the head, fades to nothing at the tail
- **Butter Mode** — ultra-smooth high-framerate tracking
- Full-desktop overlay, multi-monitor aware
- Click-through transparent window — never blocks input or the taskbar
- System tray icon with toggle on/off and quick settings
- Launch at Windows startup option

### Laser Dot
- Color picker (color wheel + RGB/HEX + opacity)
- Size slider
- Shape presets: Circle, Star, Diamond, Crosshair
- Custom dot image (PNG/SVG)
- Pulse animation — rhythmic size oscillation
- Spin animation — rotation for custom images

### Laser Trail
- Independent trail color with gradient mode (two-color head-to-tail shift)
- Trail length, thickness, and taper profile controls
- Fade styles: Alpha only, Thin-and-fade, Glow-dissolve
- Smoothness slider (independent of Butter Mode)
- Trail patterns: Fire, Electric, Smoke, Plasma

### Glow
- Dot glow: radius, intensity, and bloom style (Soft/Hard/Pulse)
- Tail glow: width, intensity, and falloff along the full trail length

### Click Effects
- Left click, right click, middle click, and double-click effects — each independently configured
- Styles: Ripple, Burst, Shockwave, Sparkle, or custom image
- Per-button color, size, and duration

### Draw Mode
- **Hold a key** to draw persistent strokes on screen
- **Double-tap the key** to clear all strokes
- Configurable keybind (default: Left Ctrl)

### Profiles & Presets
- Create, rename, and delete named profiles (e.g. "Presentation", "Stream", "Gaming")
- Export and import profiles as `.json` files (shareable anywhere)
- Built-in starter presets: Classic Red, Blue Ice, Golden, Neon Green, Ghost, Cyberpunk

---

## Usage

1. Run `LaserCursorApp.exe` — it starts silently in the system tray
2. Right-click the tray icon to toggle, open Options, or exit
3. Open **Options** to customize every aspect of the laser
4. Hold **Left Ctrl** to draw on screen; double-tap to clear

---

## Build from Source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8).

```bash
git clone https://github.com/HoodBlah/laser-cursor-app.git
cd laser-cursor-app/LaserCursorApp
dotnet build
```

To produce a self-contained single-file exe:

```bash
dotnet publish LaserCursorApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Requirements

- Windows 10 or 11 (x64)
- No additional dependencies — the exe is fully self-contained
