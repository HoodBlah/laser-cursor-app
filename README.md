# Laser Cursor

A lightweight Windows overlay that replaces your cursor with a glowing laser trail — built for presentations, screen recordings, and streaming.

---

## Download

Grab the latest release from the [Releases page](https://github.com/HoodBlah/laser-cursor-app/releases/latest).

No installation required — just run `LaserCursorApp.exe`.

---

## Features

- **Laser trail** — smooth Catmull-Rom spline with configurable color, length, thickness, taper, and fade style
- **Dot customization** — color, size, shape presets (circle, star, diamond, crosshair), custom image, pulse and spin animations
- **Glow effects** — independent dot glow and full-trail bloom with adjustable radius and intensity
- **Trail patterns** — Fire, Electric, Smoke, and Plasma overlays
- **Click effects** — ripple, burst, shockwave, or sparkle animations on left, right, middle, and double-click
- **Draw Mode** — hold a configurable key to draw persistent freehand strokes on screen; double-tap the key to clear
- **Profiles** — save, load, export, and import named configurations as `.lasercfg` files
- **Built-in presets** — Classic Red, Blue Ice, Golden, Neon Green, Ghost, Cyberpunk
- **Click-through overlay** — never blocks your input or covers the taskbar
- **Multi-monitor** — spans the full virtual desktop automatically
- **Launch at startup** — optional auto-run via Windows registry

---

## Getting Started

1. Run `LaserCursorApp.exe` — it starts in the system tray with no visible window
2. The laser trail appears immediately on your cursor
3. Right-click the tray icon to access options or toggle the overlay on/off

---

## Draw Mode

Hold the designated key (default: **Left Ctrl**) to draw persistent freehand strokes on screen.  
Double-tap the key quickly to clear all drawn strokes.  
The keybind and double-tap threshold are configurable in **Options → Trail → Draw Mode**.

---

## Options

Open the Options window from the tray icon to customize everything:

| Tab | What you can change |
|-----|---------------------|
| **Dot** | Color, size, shape, custom image, pulse, spin |
| **Glow** | Dot glow color, radius, intensity, bloom style |
| **Trail** | Color, gradient, length, thickness, taper, fade style, smoothness, patterns, draw mode |
| **Tail Glow** | Trail bloom color, width, intensity, falloff |
| **Clicks** | Per-button effects (left, right, middle, double-click) |
| **General** | Profiles, import/export, startup, auto-hide |

---

## System Requirements

- Windows 10 or 11 (64-bit)
- .NET 8 runtime is bundled — no separate install needed

---

## Building from Source

```bash
git clone https://github.com/HoodBlah/laser-cursor-app.git
cd laser-cursor-app
dotnet publish LaserCursorApp/LaserCursorApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
