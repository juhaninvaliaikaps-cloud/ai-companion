# 2D video avatar

An optional avatar mode that shows one looping, full-body video clip in the overlay window in place of the 3D model (JUH-99). The 3D avatar is the default and is not changed by this mode.

Only the idle clip is played. Talking and reaction clips are planned, and the file naming below already leaves room for them.

**Not yet verified in a Windows build.** The logic is covered by EditMode tests. Playback, transparency, and the shader have not been run. See [What to check on Windows](#what-to-check-on-windows).

## Turn it on and off

Settings (**F12**) → **Avatar**:

- **Avatar: 2D video** shows the clip.
- **Avatar: 3D model** shows the 3D avatar again. Nothing has to reload, because the model was only hidden.

The choice is saved as `"avatarMode": "video"` or `"3d"` in `%APPDATA%\ProjectLeia\settings.json`. Deleting that line, or setting it to `3d`, also returns to the 3D avatar.

If the folder or the clip is missing, or the clip cannot be decoded, the 3D avatar stays on screen and the Avatar tab says why.

## Files

Clips are read from `%APPDATA%\ProjectLeia\video-avatar\`. **Video folder** on the Avatar tab names another folder. Only local folders are accepted. A web address is ignored.

The clips are yours and stay on your PC. They are not part of this repository.

| File | Meaning |
| --- | --- |
| `idle.webm`, `idle.mp4`, `idle.mov`, `idle.m4v` | The picture. If several exist, the first in this order is used. |
| `idle.mask.mp4` (or `.webm`, `.mov`, `.m4v`) | A mask clip of the same size, length, and frame rate. White shows her. Black is see-through. Grey is partly see-through. |
| `idle.mask.png` (or `.jpg`) | A still mask. It only suits a clip in which her outline does not move. |
| `idle.sbs.mp4` | A packed clip: the picture in the left half and the mask in the right half of one file. |

Which transparency source is used:

1. Picture plus mask clip.
2. Picture plus mask picture.
3. Packed clip.
4. Picture alone. Its own alpha channel is used. A clip with no alpha channel is fully opaque, so its background covers the desktop behind it.

For a clip with a gradient background you therefore need a mask (rows 1 to 3) or an alpha-enabled clip (row 4).

The packed clip is the safest choice, because one decoder produces both halves and they cannot drift apart. With a separate mask clip, two decoders run. The mask is moved back onto the picture's frame when it is more than one frame off, at most twice a second.

### Preparing files with ffmpeg

These commands are suggestions. They have not been run against the idle clip.

Remove the sound and make an H.264 file that Windows can decode:

```text
ffmpeg -i source.mp4 -an -c:v libx264 -pix_fmt yuv420p idle.mp4
```

Pack a picture and a mask clip into one file:

```text
ffmpeg -i idle.mp4 -i idle.mask.mp4 -filter_complex hstack=inputs=2 -an -c:v libx264 -pix_fmt yuv420p idle.sbs.mp4
```

The mask itself comes from a matting tool (for example a background-removal or rotoscoping feature in a video editor), exported as a black-and-white clip.

## Sound

The clip's sound is never played. The player is created with `VideoAudioOutputMode.None`, so no audio track is routed anywhere. Speech from the companion is a separate path and is not affected.

## Size and position

On the Avatar tab, in 2D video mode:

| Control | Range | Meaning |
| --- | --- | --- |
| Video size | 20% to 100% | 100% is the largest size that fits inside the window with the clip's own aspect ratio. The clip is never stretched. |
| Video left or right | −50% to 50% | Sideways shift as a share of the window width. |
| Video up or down | −30% to 30% | Vertical shift as a share of the window height. A negative value sinks the clip below the window edge. Use it when the clip has empty space under her feet. |
| Reset video size and position | | Back to 100%, centred, on the bottom edge. |

To make her larger than the window allows, raise **Size** on the Overlay tab. That grows the window, and the clip grows with it.

The values are saved as `videoAvatarSize`, `videoAvatarOffsetX`, and `videoAvatarOffsetY`.

## How it works

- `VideoAvatar` (in `Scripts/Settings/VideoAvatar`) is added to the `AppBootstrap` object by `SettingsPanel` at startup. The scene file is not changed. In 3D mode it does nothing.
- The clip is drawn by a uGUI `RawImage` on its own canvas, behind the portrait, the chat panel, and the settings panel. The image is **not** a raycast target and the canvas has no raycaster. Click-through, drag, double-click, and the scroll wheel therefore still depend on the alpha of the pixel under the cursor, the same as for the 3D model. The see-through part of the clip lets clicks reach the desktop.
- `Resources/VideoAvatar.shader` combines the picture with the mask and writes premultiplied colour and alpha, so the desktop shows through without a dark fringe. If the shader cannot be loaded, the clip is drawn without a mask and a warning is logged.
- While a clip is on screen, `AvatarMount.SetModelHidden(true)` stops the 3D model and the placeholder capsule from being drawn (`Renderer.forceRenderingOff`). The model stays loaded and its Animator keeps running. The animation director, lip sync, and the face code keep working against it, unseen.
- The 3D model is hidden only in the `Playing` state. In every other state it is visible, so a broken clip never leaves an empty window.
- Gravity and the taskbar line: the clip stands on the bottom edge of the window, so `VideoAvatar` sets the foot offset to 0 after `FootGrounding` and before `DesktopPresence` each frame.
- Chat, voice, memory, private mode, and the tray do not know which avatar is showing. No clip or frame is sent anywhere.

Plain-logic classes, all tested: `AvatarModeNames`, `VideoAvatarFiles`, `VideoAvatarFolders`, `VideoAvatarLayout`, `VideoMaskSync`, `VideoAvatarStatus`.

## Known behaviour in this first version

- **No talking or emotion in the clip.** She loops the idle clip while she speaks. Cues from the model still drive the hidden 3D face and the expression portrait, if that is on.
- **Walking slides.** When the model asks her to walk, the window moves and the idle clip slides with it, because there is no walking clip.
- **Rotate, zoom, and Ctrl+scroll** still act on the hidden 3D model. Zoom and rotation are not visible on the clip. Ctrl+scroll above 100% grows the window and the clip with it.
- **The expression portrait** still sits next to where the 3D head would be.
- **Idle frame rate.** After a few seconds without the pointer the overlay drops to 15 fps, and the clip is shown at that rate.
- **The hidden 3D model still animates**, which costs a little CPU.

## Adding talking and reaction clips later

The lookup is by clip name: `VideoAvatarFiles.Find(files, "talk")` already finds `talk.mp4`, `talk.mask.mp4`, and `talk.sbs.mp4`. A later change needs to:

1. Add names to `VideoAvatarClipNames` (for example `talk`, and one per emotion).
2. Prepare one player per clip in `VideoAvatar`, and switch which one is drawn.
3. Choose the clip from `IAvatarAnimator.IsTalking` and `IAvatarAnimator.Emotion`, which the hidden 3D path already keeps up to date.

## What to check on Windows

None of these has been run yet.

1. **Playback.** Put the clip and its mask in `%APPDATA%\ProjectLeia\video-avatar\`, choose **Avatar: 2D video**. The Avatar tab should say `Playing idle.mp4 ...`, and the log should have `VideoAvatar: playing idle.mp4 WxH mask=...`.
2. **Loop.** The clip loops without a visible jump or a black frame at the loop point.
3. **Sound.** No breathing or other sound from the clip. The companion's voice still plays.
4. **Aspect ratio.** She is not stretched at any size, and after changing the window size on the Overlay tab.
5. **Transparency.** The desktop shows around her. The gradient background is gone. Look for a dark or light fringe at her outline, and for a hard or ragged edge.
6. **Mask timing.** With a separate mask clip, the outline does not lag behind her after a few minutes. With a packed clip, check for a thin line at her right edge.
7. **Click-through.** Clicks on the see-through part reach the desktop. Clicking her focuses the window. Dragging her moves the window. Double-clicking her opens the chat.
8. **Controls.** The three sliders move and scale her immediately, and the values survive a restart.
9. **Ground.** After a drag she falls and lands with the bottom of the clip on the taskbar line.
10. **Switch back.** **Avatar: 3D model** shows the 3D avatar at once, with feet on the taskbar and the face reacting. Switch a few times.
11. **Missing clip.** Rename the clip, press **Reload video**. The 3D avatar shows and the tab names the problem. Rename it back and press **Reload video**.
12. **Everything else.** Chat, voice, push-to-talk, private mode, tray pause, and the settings panel behave as before in both modes.
13. **Alpha clip.** If you have a WebM with alpha, check that it is see-through with no mask file. Unity documents VP8 WebM as the format that carries alpha. Whether that works for a file played from a folder at runtime on Windows is not confirmed.

## Tests

`VideoAvatarTests` covers the mode names, the settings fields (defaults, old files, write and read, clamping), the file lookup and its priority, the folder choice, the layout (aspect ratio, size, offsets, clamping), the mask sync rule, the status text, and that the shader loads with the properties the script sets.
