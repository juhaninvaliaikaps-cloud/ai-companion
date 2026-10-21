# Windows overlay

The desktop window is a borderless, transparent, always-on-top player. The project uses the **built-in render pipeline** (no URP asset). `Main` contains `AppBootstrap`, `OverlayWindow`, a camera, a light, and a pink-tinted capsule named **Placeholder Avatar**. The capsule stands in for the real model.

Win32 calls are compiled only for the Windows player (`UNITY_STANDALONE_WIN && !UNITY_EDITOR`). The editor and the Linux CI test run still compile. EditMode covers the click-through decision, drag math, pointer mapping, frame-rate choice, camera clear, and the `AppBootstrap` startup hook. The transparent window itself is checked on Windows.

## Player settings

Changed for this window:

| Setting | Value | Why |
| --- | --- | --- |
| Use DXGI Flip Model Swapchain | Off | Per-pixel alpha needs the blit-model swapchain. |
| Graphics API (Windows) | Direct3D11 only | DWM per-pixel alpha is the D3D11 path. |
| Default screen size | 480×720 | Companion-sized window. The player also calls `Screen.SetResolution` with `FullScreenMode.Windowed`. |
| Allow Fullscreen Switch | Off | Alt+Enter stays in windowed mode. |

Already set, and the overlay depends on them:

| Setting | Value |
| --- | --- |
| Preserve Framebuffer Alpha | On |
| Fullscreen Mode | Windowed |
| Run In Background | On |
| Visible In Background | On |
| Resizable Window | On |
| Color Space | Linear |
| Use 32-bit Display Buffer | On |
| Active Input Handling | Both |

The window style is `WS_POPUP`, so the player has a sized window with no title bar. `Resizable Window` stays on so Unity still allows a later resize. This build moves by dragging the capsule or the debug label. Windowed mode keeps that drag on the desktop.

At runtime the camera clears to transparent black `(0, 0, 0, 0)` with HDR and MSAA off, `vSyncCount` is 0, and `targetFrameRate` is 60. After 3 seconds with the pointer outside the window it drops to 15. `DwmExtendFrameIntoClientArea` uses margins of -1. Click-through toggles `WS_EX_LAYERED` and `WS_EX_TRANSPARENT` when the cursor is not over the capsule collider, a uGUI object, the debug label, or an opaque framebuffer pixel.

If the built window stays black, set **Player Settings > Other Settings > Color Space** to **Gamma** and rebuild. Linear is what this project ships.

## Build and run

The Editor must be open on this project (Unity 6000.6.6f1) with Windows Build Support (Mono).

```text
unity command build --target StandaloneWindows64 --outputPath Builds/Windows/AICompanion.exe --options Development --confirm true
unity command build_status
```

Poll `build_status` until `status` is `completed`. `build_windows_player` queues the same player. Then run `Builds/Windows/AICompanion.exe`. `Builds/` is gitignored.

The Unity splash is opaque. After it closes, the window should be transparent except for the capsule and a small debug label in the top-left of the window. The window opens toward the bottom-right of the primary monitor.

## What to check

1. **Transparency.** The desktop shows through around the capsule. Only the capsule and the debug label are visible. There is no title bar.
2. **Topmost.** The window stays above other apps. Click the capsule so the window has focus, then press **F8**. The label switches between `Topmost on` and `Topmost off`. F8 again turns always-on-top back on.
3. **Drag.** Press the left button on the capsule or on the debug label and move the mouse. The window follows. Release to drop it.
4. **Click-through.** Click the desktop in the empty part of the window. The click reaches the app behind it. Click the capsule. The capsule receives the click (the window focuses, and a drag can start).
5. **Frame rate.** The label shows about 60 fps while the pointer is inside the window. Move the pointer off the window, wait about 3 seconds, and the label should fall to about 15 fps. Move back onto the window and it returns to about 60.

Transparency, topmost, drag, and click-through are properties of the Windows player. Play mode in the editor shows the capsule and the label, and F8 flips the label there.
