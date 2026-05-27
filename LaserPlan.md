# Laser Cursor — Feature Plan
---

## Basic Features

- ✅ Red laser trail with smooth Catmull-Rom curve rendering
- ✅ Dynamic tapered trail (thick at head, fades to nothing at tail)
- ✅ Butter Mode for ultra-smooth high-framerate tracking
- ✅ System tray toggle (on/off)
- ✅ Multi-monitor aware full-desktop overlay
- ✅ Click-through transparent window (never blocks input)
- ✅ Launch at startup option (tray menu)

---

## Advanced Features

Accessed via a dedicated
**Options** window opened from the tray icon. Settings persist across sessions. A live
preview panel updates in real-time as values are adjusted. Settings can be exported and
imported as `.json` preset files.

---

### 1. Laser Dot Customization

Customize the appearance of the dot at the tip of the laser.

- ✅ **Color** — color wheel picker + RGB/HEX input + opacity slider
- ✅ **Size** — slider (small → large, in px)
- ✅ **Shape presets** — Circle (default), Star, Diamond, Crosshair
- ✅ **Custom image** — import any PNG/SVG as the dot; adjustable size and transparency
- ✅ **Pulse animation** — optional rhythmic size oscillation; speed and intensity adjustable
- ✅ **Spin animation** — optional rotation for custom dot images; speed and direction adjustable

---

### 2. Dot Glow

An optional soft radial glow rendered behind the dot.

- ✅ **Enable/disable** toggle
- ✅ **Color** — independent color wheel + RGB/HEX input (can differ from dot color)
- ✅ **Radius** — size of the glow halo in px
- ✅ **Intensity/opacity** — how bright and visible the glow appears
- ✅ **Bloom style** — Soft (Gaussian), Hard (sharp edge), Pulse (synced to dot pulse if enabled)

---

### 3. Laser Tail Customization

Fine-grained control over the trailing streak behind the cursor.

- ✅ **Color** — color wheel + RGB/HEX input + opacity slider
- ✅ **Gradient mode** — set a second color so the tail shifts hue from head to tail
- ✅ **Length** — how long the trail persists before fading (ms value or Short/Medium/Long preset)
- ✅ **Thickness** — max width at the head end in px
- ✅ **Taper profile** — how quickly the trail narrows (linear, exponential, or custom curve)
- ✅ **Fade style** — Alpha only, Thin-and-fade (default), or Glow-dissolve
- ✅ **Smoothness** — dedicated slider for Catmull-Rom subdivision density, independent of Butter Mode
- ✅ **Trail animation patterns** — style overlays applied on top of the base trail: Fire, Electric, Smoke, Plasma; each has speed and intensity controls

---

### 4. Tail Glow

An optional bloom rendered along the full length of the trail.

- ✅ **Enable/disable** toggle
- ✅ **Color** — independent color wheel + RGB/HEX input
- ✅ **Width** — how far the glow extends beyond the trail edges in px
- ✅ **Intensity/opacity** — brightness of the bloom
- ✅ **Falloff** — how quickly the glow fades laterally (soft vs. sharp)

---

### 5. Particle Falloff

Sparks or images that spawn along the trail, sit stationary, and fade out independently —
like embers dropping off the laser.

- **Enable/disable** toggle
- **Particle image** — built-in shapes (Spark, Dot, Star, Ring) or custom PNG import
- **Spawn rate** — frequency of particle spawning along the trail
- **Particle size** — base size in px; optional random variance range
- **Fade duration** — how long each particle takes to disappear after spawning
- **Drift** — optional motion while fading; direction, speed, and gravity individually adjustable
- **Opacity curve** — linear fade vs. quick flash then slow dissolve

---

### 6. Click Effects

Visual effects triggered by mouse button presses, rendered at the click position.

- ✅ **Left click effect**
  - Enable/disable toggle
  - Effect style: Ripple, Burst, Shockwave, Sparkle, or custom image
  - Color, size, and duration adjustable
  - Optional secondary pulse ring

- ✅ **Right click effect**
  - Independent style, color, and size from left click
  - Effect style: same options as left click, defaults to a different preset
  - Optional label flash (brief text overlay, e.g. "Right Click")

- ✅ **Middle click effect**
  - Lightweight separate effect (e.g. small crosshair burst)
  - Color, size, and duration adjustable

- ✅ **Double-click effect**
  - Optional distinct layered animation on rapid double-click detection
  - Intensity multiplier relative to single click effect

---

### 7. Cursor State Effects

Custom overlays for each Windows cursor state, replacing or augmenting the system cursor
appearance while the app is active.

- **Normal (arrow)** — the default laser dot + tail (already handled by Basic)
- **Loading (spinning circle)** — custom spinner animation overlay; color, size, speed adjustable
- **Working in background (arrow + spinner)** — secondary animated indicator alongside the dot
- **Text / I-beam** — custom beam style; color and thickness adjustable
- **Hand / link hover** — custom pointer image or animated hand; size adjustable
- **Horizontal/Vertical resize** ↔↕ — colored directional arrows or lines; color and size adjustable
- **Diagonal resize** ↗↙↖↘ — matching diagonal variants
- **Move (four-way arrow)** — custom move indicator overlay
- **Precision / crosshair** — custom crosshair design; color, line weight, gap adjustable
- **Unavailable / blocked** — custom "no" indicator; color adjustable
- **Help (question mark arrow)** — custom help pointer

Each state has:
- Enable/disable toggle (fall back to Windows default if disabled)
- Custom image import (PNG/SVG) as an alternative to built-in styles
- Size and opacity controls

---

### 8. Profiles & Presets

Save and switch between complete configurations instantly.

- ✅ Create, rename, and delete named profiles (e.g. "Presentation", "Stream", "Gaming")
- **Quick-switch via tray icon submenu**, no need to open Options
- ✅ Export profile as `.lasercfg` file for sharing
- ✅ Import `.lasercfg` from others
- ✅ Built-in starter presets: Classic Red, Blue Ice, Golden, Neon Green, Ghost (white/dim), Cyberpunk
- **Custom themes** — themed color + effect bundles that restyle the entire UI palette and defaults at once (e.g. "Cyberpunk", "Pastel", "Monochrome"); distinct from profiles in that a theme affects aesthetic defaults rather than specific slider values; importable as `.lasertheme` files

---

### 9. Display & Accessibility

- **Hide system cursor** — optionally suppress the real Windows cursor so only the laser dot shows
- **Per-monitor DPI scaling** — correctly scales overlay on mixed-resolution multi-monitor setups
- **Invert mode** — renders laser in a color that maximizes contrast against the screen below
- **Reduced motion** — disables all pulse/spin/particle/click animations for accessibility
- **Auto-hide** — automatically fades the laser out after a configurable idle timeout (0.5 s – 30 s); reappears instantly on movement; timeout and fade duration both adjustable

---

### 10. Startup & System Integration

- ✅ **Launch at Windows startup** — optional auto-run on login via registry
- ✅ **Start minimized to tray** — no window shown on launch
- **Restore last active profile on startup**

---

### 11. Window Highlighting

Draws a colored border or glow around whichever window is under the cursor, making the active target obvious during demos.

- **Enable/disable** toggle
- **Highlight style** — Outline border, Inner glow, Corner brackets, or Full tint
- **Color** — color picker with opacity
- **Border thickness** — width of the highlight frame in px
- **Activation threshold** — how long cursor must hover before the highlight appears (prevents flicker)
- **Exclude list** — windows or process names to never highlight (e.g. the taskbar)

---

### 12. RGB Peripheral Sync

Mirrors the active laser color to supported RGB peripherals in real time.

- **Enable/disable** toggle per device category
- **Supported SDKs** — Razer Chroma, Corsair iCUE, Logitech GHUB, OpenRGB (catch-all)
- **Sync mode**
  - *Match dot color* — peripherals match the current dot color
  - *Match trail color* — peripherals match the current trail color
  - *Reactive* — color pulses on click or movement, returns to idle color
  - *Rainbow sync* — peripherals cycle through ROYGBIV in time with the rainbow trail
- **Brightness** — independent brightness multiplier for peripherals
- **Devices** — checklist of discovered devices; individual enable/disable per device

---

### 13. Recording Mode & OBS Integration

Optimizes the laser for screen capture and live streaming.

- **Recording mode** toggle — switches to a rendering profile tuned for capture:
  - Boosts trail opacity and thickness so it reads clearly on compressed video
  - Disables effects that don't capture well (e.g. subtle glow on dark backgrounds)
  - Optionally locks framerate to match the capture framerate
- **OBS integration**
  - Exposes a named window source (`Laser Cursor Overlay`) that OBS can capture as a transparent layer without capturing the rest of the screen
  - Provides a WebSocket command (`laser.toggle`, `laser.setProfile`, `laser.spotlight`) so OBS scene scripts or Stream Deck buttons can control the laser
  - Optional: receive scene-change events from OBS to auto-switch laser profiles per scene
- **Chroma key output** — renders the overlay on a solid chroma-key background (configurable color) for use in video editing pipelines that require a keyed layer
