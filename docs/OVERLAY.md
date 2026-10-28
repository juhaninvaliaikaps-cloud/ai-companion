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

The camera looks along +Z and reframes after the animator. Humanoid bones (head, chest, hips, arms, hands, feet, and toes) define the fit, and sane skinned-mesh bounds are included so hair is not left outside the frame. A model that is not a humanoid uses its renderer corners instead. The fit reserves space under the lowest point so the toes are not on the last pixel, and it follows clips and yaw. Scroll the wheel over a visible pixel to zoom toward that point. Zoom eases in and out. Fully out is the full-body fit and cannot go farther. Fully in stops before the camera enters the mesh, so the body does not cull down to the boots. Middle-click on a visible pixel returns to the full-body fit and still clears the extra yaw. Hold **Ctrl** and scroll over a visible pixel to scale her, not the chat panel or the debug labels. Those stay at their normal pixel size. The window stays 480×720 while she gets smaller, with her feet on the ground. Above 100% the transparent window grows with her so she is not clipped, keeps the 2:3 shape, stays inside the monitor work area (above the taskbar), and keeps the bottom-right corner from the last drag. Empty pixels still click through, so the wheel does nothing there. The chat panel clamps inside the window. Size and zoom are saved in `PlayerPrefs` (`ai-companion.window-scale`, `ai-companion.camera-zoom`). The editor Game view does not resize. The size percentage is on the debug label, and her model scale changes in that fixed view, so a size well above 100% can meet the Game view edge. The Windows player grows the window instead. A `Camera:` line in the Console lists zoom, size, near, far, the culling mask, and every avatar renderer (enabled, visible, layer, bounds).

If the built window stays black, set **Player Settings > Other Settings > Color Space** to **Gamma** and rebuild. Linear is what this project ships.

## Build and run

The Editor must be open on this project (Unity 6000.6.6f1) with Windows Build Support (Mono).

```text
unity command build --target StandaloneWindows64 --outputPath Builds/Windows/BackseatCatgirl.exe --options Development --confirm true
unity command build_status
```

Poll `build_status` until `status` is `completed`. `build_windows_player` queues the same player. Then run `Builds/Windows/BackseatCatgirl.exe`. `Builds/` is gitignored.

The Unity splash is opaque. After it closes, the window should be transparent except for the avatar (or the capsule, if no model loaded), a small debug label in the top-left of the window, and the chat panel when it is open (**F10**). The window opens toward the bottom-right of the primary monitor, inside the work area and above the taskbar. **F7** cycles the local prefab, the CC0 VRM, and the CC0 FBX.

## System tray

The Windows player adds a notification-area icon when it launches and removes it on quit, including `Application.quitting` and process exit. The icon is a generated rose tile. There is no imported texture. The message loop runs on a background thread. Menu actions are applied on the Unity thread.

Left-click shows or hides the overlay. Right-click opens the menu:

| Item | Action |
| --- | --- |
| Show companion / Hide companion | Shows or hides the borderless window. |
| Open chat | Shows the overlay and opens the chat panel (same path as **F10** opening it, or a double-click). |
| Settings | Opens the settings panel in this window (also **F12**). It does not change transparency, topmost, or the click-through mode. |
| Pause | Check mark. Mutes speech without changing the **F6** mute pref, blocks push-to-talk, and drops animation cues back to neutral. Saved in `PlayerPrefs` (`ai-companion.paused`). |
| Start with Windows | Check mark. Per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `Backseat Catgirl`, set to the quoted exe path. Unchecking deletes that value. A leftover `AI Companion` value is copied over and removed on launch. |
| Quit | Removes the icon and quits. |

The taskbar button is hidden after the player HWND has been stable for a short time (`WS_EX_TOOLWINDOW`, and `WS_EX_APPWINDOW` cleared). Hiding it earlier would make `MainWindowHandle` go empty before the overlay locks that handle. The default is on. Uncheck **Hide Taskbar Button** on the `CompanionTray` component, or set `PlayerPrefs` `ai-companion.hide-taskbar` to `0`, and rebuild or relaunch. The editor does not create a tray icon. Play mode still honors a saved pause flag.

## What to check

1. **Transparency.** The desktop shows through around the model. The model, the debug label, and the open chat panel are visible. There is no title bar. MToon or URP Lit should not turn the whole window into a solid plate.
2. **Topmost.** The window stays above other apps. Click the model so the window has focus, then press **F8**. The label switches between `Topmost on` and `Topmost off`. F8 again turns always-on-top back on.
3. **Drag.** Press the left button on the model or on the debug label and move the mouse. The window follows. Release to drop it. This stays the case while rotate mode is off. While rotate mode is on, a left-drag that starts on her visible pixels yaws her instead, and a left-drag on the debug label still moves the window.
4. **Click-through.** Click the desktop in the empty part of the window, including the space close to the model but not on a visible pixel. The click reaches the app behind it. Click the model on her body, hair, or clothes. The window focuses, and a drag can start. The invisible box around the mesh must not catch clicks.
5. **Frame rate.** The label shows about 60 fps while the pointer is inside the window. Move the pointer off the window, wait about 3 seconds, and the label should fall to about 15 fps. Move back onto the window and it returns to about 60.
6. **Models.** **F7** switches between the local prefab (Asuna, once assigned) and the two CC0 samples. The Console line `Avatar ready` lists blendshapes. If every source is missing, the pink capsule is back and dragging uses the capsule.
7. **Animator.** The debug label also shows the emotion and `Talk on` or `Talk off`, plus `1-5 emotion   T talk   F10 chat`. Those keys do not use **F7** or **F8**. After the frame rate drops to about 15, the avatar keeps moving. The label still catches clicks on itself. Empty space around the model still clicks through. The chat panel catches its own clicks and does not drag the window.
8. **Rotate.** Right-click a visible pixel. The label switches to `Rotate: on (right-click to finish)`. Drag horizontally with the left button. She turns around vertical. Right-click her again, or press **Esc**, and the label returns to `Rotate: off`. Left-drag then moves the window. Middle-click a visible pixel, or press **R** during rotate mode, faces her toward the camera again. Middle-click also returns the camera to the full-body view. A right-click on empty pixels does nothing. The chosen yaw is remembered per avatar source.
9. **Tray.** After the splash, a rose icon sits in the notification area. Left-click hides the companion. Left-click again shows it, still borderless and topmost. Right-click the tray icon and choose **Open chat**. The panel opens. **Pause** checks, speech stops, and holding the push-to-talk key does nothing until pause is cleared. **Start with Windows** checks, and Task Manager or `reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Run` shows `Backseat Catgirl`. Choose it again and that value is gone. **Settings** opens the settings panel. **F12** does the same. Empty space around that panel still clicks through, and the desktop behind the avatar is unchanged. **Quit** exits, and the icon is gone without hovering the tray. The taskbar does not keep a button for the player once the window has been up for about a second. The same switch is on the settings Overlay tab (`ai-companion.hide-taskbar`, also stored in `%APPDATA%\ProjectLeia\settings.json`).
10. **Frame, zoom, and size.** Head, hair, hands, and the tips of the feet stay inside the window while Idle plays and after a yaw. Scroll the wheel over her body. The view eases toward that spot, and the label shows `Zoom` above a few percent. Zoomed in, her body stays visible with the boots. It does not collapse to only the boots. Scroll back, or middle-click her, and the full body is in frame again, including the hair and the feet. Scrolling the empty part of the window does nothing (that click still goes through). Hold **Ctrl** and scroll over her. The label `Size` changes and she gets bigger or smaller. The chat panel and the debug labels stay the same pixel size. Below 100% the window stays put and her feet stay on the ground. Above 100% the transparent window grows so she is not clipped, that corner stays put, and the window stays above the taskbar. In the Editor the Game view cannot grow, so a large size can reach the view edge. Quit and relaunch. The size and the zoom come back. Open the chat panel at the large size and at the small size. It stays the same size and stays inside the window. If hair or the body is still missing, copy the `Camera:` line from the Console or from `%USERPROFILE%\AppData\LocalLow\AI Companion\AI Companion\Player.log`.
11. **First-run setup.** On a Windows account with no `%APPDATA%\ProjectLeia\settings.json` and no `%APPDATA%\ProjectLeia\secrets.json`, the player shows a five-step setup in this same window before the companion is left on her own. There is no second window and no main menu. The title says **Backseat Catgirl**. Welcome names what goes to xAI (Grok chat and xAI text-to-speech) and what stays local (Ollama private mode; that content is not sent to cloud TTS, the voice agent, or Uthana). AI accepts a masked xAI key, tests the connection, or skip continues on the local model. The key is written only to `secrets.json` under that AppData folder. It is not in `settings.json`, the player folder, or the log, and the field is empty after a save. Avatar lists Auto, Local prefab, VRM sample, and FBX sample. Personality saves a name and friend or girlfriend. Controls lists the tray icon, drag, rotate mode, and the same keys as Settings, Controls. Back, Next, and Skip work. Skip uses that step's default. Done writes `settings.json` and closes the wizard. Relaunch does not show it. An account that already had `settings.json` or a saved key before this build does not show it either. To show it again, launch `Builds\Windows\BackseatCatgirl.exe --setup`, or open Settings, Privacy, **Run setup again**. Deleting only `settings.json` is not enough while `secrets.json` still holds a key. Opening and closing the wizard does not change transparency, topmost, or the click-through mode. Empty pixels outside the wizard still click through.
12. **Expression portrait.** Settings → Avatar → **Show expression portrait** is off until you turn it on. With it on and no `%APPDATA%\ProjectLeia\portraits` folder, the frame stays hidden and the Console logs one warning. Drop the PNGs in that folder (see [PORTRAITS.md](PORTRAITS.md)). A rounded picture sits beside her head, swaps with the emotion keys, blinks when the set has blink frames, and opens the mouth while she talks. A 448×504 still and a 320×360 frame both fit without stretching. Click the empty window around the frame. The click still reaches the desktop. Quit and relaunch. The switch is still on, and `settings.json` has `"showExpressionPortrait": true`.

Transparency, topmost, drag, and click-through are properties of the Windows player. Play mode in the editor shows the capsule and the label, and F8 flips the label there.

## Desktop gravity and walking

The overlay window follows the monitor work area: the top of the taskbar is the ground, and a work-area change (taskbar moved, monitor switched) is re-read every frame through IWindowChrome.TryGetWorkArea.

- Dragging pauses the motion. When the button is released the window stays where the pointer left it, then falls from there to the ground with a short acceleration and no bounce.
- While idle on the ground she pauses for a few seconds, walks left or right for a few seconds, stops, and repeats. Edges clamp: at the left or right work-area edge she stops instead of leaving the screen.
- Walking and falling raise the overlay to the active frame rate through IOverlayKeepAwake; once she is idle on the ground it drops back to the idle rate.
- The motion is screen-space arithmetic in DesktopPresenceModel with no Rigidbody, so any avatar works and nothing depends on root motion.

The walk and fall poses come from the animator parameters Walking and Falling. The shared controller on this branch declares neither; the gesture layer and the Walk clip land with JUH-65 (PR #18). Until that merges she slides along the ground without a walk cycle, and the parameters are ignored because AvatarAnimationController only writes a parameter the controller declares.

Not in this change: turning the body to face the walk direction, a fall or dangle clip, picking her up while dragged, and the settings toggle. Facing needs the Walk clip and a yaw offset that does not fight the saved per-avatar yaw from JUH-60, so it is a separate step.

### Feet on the ground line

Gravity moves the window, but the window edge is not her soles. FootGrounding projects the lowest of the four Humanoid foot bones (LeftFoot, RightFoot, LeftToes, RightToes) through the overlay camera, subtracts a small sole offset, and falls back to the lowest skinned-mesh bound when the rig has no foot bones. The result is a pixel offset from the window bottom, and DesktopPresenceModel adds it to the ground line so the soles, not the window edge, land on the taskbar.

The offset is measured every 0.15 s and smoothed with a half-life of 0.35 s, so breathing and idle sway cannot bob her. The lower of the two feet wins, so a walk cycle does not lift her. AvatarSelection.groundOffsetPx is a per-avatar fine-tune for a model whose bones sit off the sole; it is local to the machine, like the rest of that asset.

Contact shadow and two-bone foot IK are deliberately not in this change.
