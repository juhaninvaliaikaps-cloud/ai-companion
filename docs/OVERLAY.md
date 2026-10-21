# Windows overlay

The desktop window is a borderless, transparent, always-on-top player. The project uses **URP 17.6.0** with HDR and MSAA off so the framebuffer keeps alpha. `Main` contains `AppBootstrap`, `OverlayWindow`, `AvatarMount`, a camera, a light, and a pink capsule named **Placeholder Avatar**. `AvatarMount` replaces the capsule when a VRM, FBX, or local prefab loads. How to add a model is in [AVATARS.md](AVATARS.md).

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

The window style is `WS_POPUP`, so the player has a sized window with no title bar. `Resizable Window` stays on so Unity still allows a later resize. This build moves by dragging the visible model or the debug label. With no model loaded it drags by the visible capsule. Windowed mode keeps that drag on the desktop.

At runtime the camera clears to transparent black `(0, 0, 0, 0)` with HDR and MSAA off, post-processing off, `vSyncCount` is 0, and `targetFrameRate` is 60. After 3 seconds with the pointer outside the window it drops to 15. `DwmExtendFrameIntoClientArea` uses margins of -1. Click-through toggles `WS_EX_LAYERED` and `WS_EX_TRANSPARENT` unless the cursor is on a uGUI object, the debug label, the chat panel, or a framebuffer pixel whose alpha is at least 0.08. The chat panel is solid and is not a drag handle. Typing in it, or a reply streaming, keeps the frame rate at 60. The test reads one pixel at the end of the frame, and only when the pointer is inside the window and has moved more than a few pixels, so the 15 fps idle path does not sample. Renderer bounds are not a hit. A drag that has started keeps the window interactive until the button is released. URP is set up so opaque MToon and URP Lit materials write alpha 1. Details are in [AVATARS.md](AVATARS.md).

The camera looks along +Z and reframes after the animator. Humanoid bones (head, chest, hips, arms, hands, feet, and toes) define the fit. A model that is not a humanoid uses its renderer corners instead. The fit reserves space under the lowest point so the toes are not on the last pixel, and it follows clips and yaw. Scroll the wheel over a visible pixel to zoom toward that point. Zoom eases in and out. Fully out is the full-body fit and cannot go farther. Fully in stops around the head and upper body. Middle-click on a visible pixel returns to the full-body fit and still clears the extra yaw. Hold **Ctrl** and scroll over a visible pixel to scale the window and the avatar together. The window keeps the 2:3 shape, stays inside the monitor work area (above the taskbar), and keeps the bottom-right corner from the last drag. Empty pixels still click through, so the wheel does nothing there. The chat panel clamps inside the resized window. Size and zoom are saved in `PlayerPrefs` (`ai-companion.window-scale`, `ai-companion.camera-zoom`). The editor Game view does not resize; the size percentage is on the debug label, and the Windows player applies it.

If the built window stays black, set **Player Settings > Other Settings > Color Space** to **Gamma** and rebuild. Linear is what this project ships.

## Build and run

The Editor must be open on this project (Unity 6000.6.6f1) with Windows Build Support (Mono).

```text
unity command build --target StandaloneWindows64 --outputPath Builds/Windows/AICompanion.exe --options Development --confirm true
unity command build_status
```

Poll `build_status` until `status` is `completed`. `build_windows_player` queues the same player. Then run `Builds/Windows/AICompanion.exe`. `Builds/` is gitignored.

The Unity splash is opaque. After it closes, the window should be transparent except for the avatar (or the capsule, if no model loaded), a small debug label in the top-left of the window, and the chat panel when it is open (**F10**). The window opens toward the bottom-right of the primary monitor, inside the work area and above the taskbar. **F7** cycles the local prefab, the CC0 VRM, and the CC0 FBX.

## What to check

1. **Transparency.** The desktop shows through around the model. The model, the debug label, and the open chat panel are visible. There is no title bar. MToon or URP Lit should not turn the whole window into a solid plate.
2. **Topmost.** The window stays above other apps. Click the model so the window has focus, then press **F8**. The label switches between `Topmost on` and `Topmost off`. F8 again turns always-on-top back on.
3. **Drag.** Press the left button on the model or on the debug label and move the mouse. The window follows. Release to drop it. This stays the case while rotate mode is off. While rotate mode is on, a left-drag that starts on her visible pixels yaws her instead, and a left-drag on the debug label still moves the window.
4. **Click-through.** Click the desktop in the empty part of the window, including the space close to the model but not on a visible pixel. The click reaches the app behind it. Click the model on her body, hair, or clothes. The window focuses, and a drag can start. The invisible box around the mesh must not catch clicks.
5. **Frame rate.** The label shows about 60 fps while the pointer is inside the window. Move the pointer off the window, wait about 3 seconds, and the label should fall to about 15 fps. Move back onto the window and it returns to about 60.
6. **Models.** **F7** switches between the local prefab (Asuna, once assigned) and the two CC0 samples. The Console line `Avatar ready` lists blendshapes. If every source is missing, the pink capsule is back and dragging uses the capsule.
7. **Animator.** The debug label also shows the emotion and `Talk on` or `Talk off`, plus `1-5 emotion   T talk   F10 chat`. Those keys do not use **F7** or **F8**. After the frame rate drops to about 15, the avatar keeps moving. The label still catches clicks on itself. Empty space around the model still clicks through. The chat panel catches its own clicks and does not drag the window.
8. **Rotate.** Right-click a visible pixel. The label switches to `Rotate: on (right-click to finish)`. Drag horizontally with the left button. She turns around vertical. Right-click her again, or press **Esc**, and the label returns to `Rotate: off`. Left-drag then moves the window. Middle-click a visible pixel, or press **R** during rotate mode, faces her toward the camera again. Middle-click also returns the camera to the full-body view. A right-click on empty pixels does nothing. The chosen yaw is remembered per avatar source.
9. **Frame, zoom, and size.** Head, hands, and the tips of the feet stay inside the window while Idle plays and after a yaw. Scroll the wheel over her body. The view eases toward that spot, and the label shows `Zoom` above a few percent. Scroll back, or middle-click her, and the full body is in frame again, including the feet. Scrolling the empty part of the window does nothing (that click still goes through). Hold **Ctrl** and scroll over her. The window grows or shrinks, she stays full-body, and the label shows `Size`. Drag her to another corner, then Ctrl+scroll again. That corner stays put, and the window stays above the taskbar. Quit and relaunch. The size and the zoom come back. Open the chat panel at the large size and at the small size. It stays inside the window.

Transparency, topmost, drag, and click-through are properties of the Windows player. Play mode in the editor shows the capsule and the label, and F8 flips the label there.
