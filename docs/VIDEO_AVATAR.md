# 2D video avatar

An optional avatar mode that shows full-body video clips in the overlay window in place of the 3D model (JUH-99). The 3D avatar is the default and stays loaded. This mode only hides it.

What plays:

- a default idle loop
- optional idle variations, started only when the idle reaches its loop point
- a walk loop while the desktop presence moves the window, mirrored for the other direction
- a walk-stop, played once when the walk ends

Named reaction clips such as `happy` can be registered and are not played yet.

## What is verified and what is not

| Area | Status |
| --- | --- |
| Idle playback with a mask clip in a Windows build | Verified by Jussi on 2026-11-11, on the first version of this code |
| Clip lookup, manifest, transitions, interruptions, gait, layout, settings | Automated EditMode tests |
| C# compiles for the Windows player | CI job "Windows player script compile" |
| Idle variations, walking, walk-stop, mirroring and clip switching with the picture + mask pairs | Jussi, manual test of commit 905d671 on Windows: "work pretty well". Two faults were reported and are addressed after that commit, see the next rows |
| Expression portrait hidden while the video shows | Automated test of the rule and of every mode switch. **Not yet seen in a player** |
| Outline that slips (mask clipping) | Cause **not established**. The packed clips, the mask speed nudge and the sync log lines are **not yet run in a player**. See [Keeping the picture and the mask together](#keeping-the-picture-and-the-mask-together) |
| A walk cue from the chat model in 2D mode | Automated test from the reply text to the walk clip. In the player logs seen so far the model wrote no cue at all. See [Known behaviour](#known-behaviour) |
| Movement speed against the feet, 10 minute run | **Not run in a player.** Use the [Windows checklist](#windows-checklist) |

## Turn it on and off

Settings (**F12**) → **Avatar**:

- **Avatar: 2D video** shows the clips.
- **Avatar: 3D model** shows the 3D avatar again at once. Nothing reloads.

The choice is `"avatarMode": "video"` or `"3d"` in `%APPDATA%\ProjectLeia\settings.json`. Deleting that line returns to 3D.

The 3D avatar stays on screen, and the Avatar tab says why, when:

- the folder does not exist
- the folder has no default idle
- the default idle cannot be decoded, or does not load within 20 seconds

A problem with any other clip never falls back to 3D. That clip is left out, a warning is logged, and she carries on with the clips that work.

## Clip folder

Clips are read from the folder named in **Video folder** on the Avatar tab. An empty field means `%APPDATA%\ProjectLeia\video-avatar\`. Only local folders are accepted.

The clips are yours and stay on your PC. This repository does not contain them: `.gitattributes` has no rule for `.mp4`, and the art is the user's own.

The player never writes to the folder.

### Without a manifest: file names

Every video file that is not a mask is a clip. Its role comes from its name. Case, `-`, `_` and spaces do not matter.

| Name | Role | Default playback |
| --- | --- | --- |
| `idle` | default idle | loop |
| starts with `idle`, for example `idle_breeze`, `idle-2`, `idle3` | idle variation | once |
| starts with `idle` and contains `side`, for example `idle_side` | side idle | loop |
| starts with `walk` | walk | loop |
| starts with `walk` and contains `stop`, or starts with `stop` | walk-stop | once |
| anything else, for example `happy` | reaction (not played yet) | once |

The mask for `name.mp4` is `name.mask.mp4`, `name_mask.mp4` or `name-mask.mp4` (also `.png` or `.jpg` for a still mask). White shows her, black is see-through.

`name.sbs.mp4` is a packed clip: picture in the left half, mask in the right half. If the folder has both `name.sbs.mp4` and `name.mp4`, the packed clip is used, because one decoder produces the picture and its mask in the same frame. A manifest can still name the pair.

A clip with no mask and no packed form uses its own alpha channel. A clip with no alpha is fully opaque.

If there is no `idle` but there are idle variations, the first variation in alphabetical order becomes the default idle and the Avatar tab says so.

### With a manifest: `video-avatar.json`

Put a file named `video-avatar.json` in the clip folder to list the clips yourself. Then only the listed clips are used. File names in it are names inside the clip folder. A path is cut down to its file name.

`tools/video-avatar/leia-keyed.video-avatar.json` is a ready manifest for the clips DeepSeek delivered on 2026-11-11.

```json
{
  "variationMinSeconds": 12,
  "variationMaxSeconds": 30,
  "clips": [
    { "name": "idle", "role": "idle", "color": "idle.mp4", "mask": "idle.mask.mp4", "playback": "loop" },
    { "name": "idle_breeze", "role": "idleVariation", "color": "idle_breeze.mp4", "mask": "idle_breeze.mask.mp4", "playback": "once" },
    { "name": "walk", "role": "walk", "color": "walk.mp4", "mask": "walk.mask.mp4", "playback": "loop",
      "facing": "left", "speed": 0.27, "moveStart": 0.15, "moveEnd": 4.95, "moveRamp": 0.3 },
    { "name": "walk_stop", "role": "walkStop", "color": "walk_stop.mp4", "mask": "walk_stop.mask.mp4", "playback": "once",
      "facing": "left", "moveEnd": 3.8, "moveRamp": 0.4, "after": "idle" }
  ]
}
```

| Key | Meaning | Default |
| --- | --- | --- |
| `name` | Clip name. Must be unique. | the colour file name without its extension |
| `role` | `idle`, `idleVariation`, `walk`, `walkStop`, `sideIdle`, `reaction` | from the name, as in the table above |
| `color` | The picture file. Use a `.sbs.mp4` file here to play the packed form. | required |
| `mask` | The mask file. If it is named and missing, the clip is left out, so its background is never shown. | found by name |
| `playback` | `loop` or `once` | by role |
| `facing` | `left` or `right`: the way she faces in the file. Walking the other way mirrors the clip. | `left` |
| `scale` | Size against the default idle. Use it when a clip was rendered with the character smaller or larger. | `1` |
| `offsetX`, `offsetY` | Shift as a share of the clip's height on screen. `offsetY` puts the feet on the same line as the idle. A negative `offsetY` sinks the clip below the window edge. | `0` |
| `speed` | Walk only. Window speed in clip heights per second. | `0.3` |
| `moveStart`, `moveEnd` | Walk and walk-stop. Seconds into the clip between which her feet carry her. The window stands still outside it. `moveEnd` 0 means the end of the clip. | `0`, clip end |
| `moveRamp` | Seconds to reach full speed after `moveStart` and to lose it before `moveEnd`. | `0.25` |
| `after` | Walk-stop only. `idle` if the clip ends facing front in the idle pose, so the default idle may follow. Leave it out to hold the last frame. | hold |
| `weight` | Idle variation only. How often it is picked against the others. | `1` |
| `variationMinSeconds`, `variationMaxSeconds` | Top level. Time on the default idle before a variation is queued. | `12`, `30` |

To add a reaction later, add a clip with `"role": "reaction"` and a name such as `happy`, `concerned`, `surprised` or `annoyed`. `VideoAvatarClipSet.FindReaction(name)` finds it. Playing it is a later change.

After editing the manifest or the folder, press **Reload video** on the Avatar tab.

## Install the delivered clips for a test

This copies files into a new folder and changes one setting. Nothing is overwritten. To undo it, clear **Video folder** or choose **Avatar: 3D model**.

```powershell
$src  = "C:\Users\Jussi\Downloads\portraits\animations\keyd"
$dst  = Join-Path $env:APPDATA "ProjectLeia\video-avatar-test"
$repo = "C:\path\to\ai-companion-pr56"     # the PR #56 checkout, see below

New-Item -ItemType Directory -Force $dst | Out-Null
foreach ($name in "idle", "idle_breeze", "walk", "walk_stop") {
    Copy-Item "$src\$name.mp4", "$src\$name.mask.mp4" $dst
}
Copy-Item "$repo\tools\video-avatar\leia-keyed.video-avatar.json" (Join-Path $dst "video-avatar.json")
Get-ChildItem $dst | Select-Object Name, Length
```

Then, in the app: **F12** → **Avatar** → **Avatar: 2D video**, and set **Video folder** to the path printed by `$dst`.

Expected files in the folder: `idle.mp4`, `idle.mask.mp4`, `idle_breeze.mp4`, `idle_breeze.mask.mp4`, `walk.mp4`, `walk.mask.mp4`, `walk_stop.mp4`, `walk_stop.mask.mp4`, `video-avatar.json`.

### Packed clips: the test for the slipping outline

The packed clips use one decoder per clip, so the picture and the mask cannot be on different frames. This copies them into a second new folder. The first folder is not touched.

```powershell
$dst2 = Join-Path $env:APPDATA "ProjectLeia\video-avatar-packed"
New-Item -ItemType Directory -Force $dst2 | Out-Null
foreach ($name in "idle", "idle_breeze", "walk", "walk_stop") {
    Copy-Item "$src\$name.sbs.mp4" $dst2
}
Copy-Item "$repo\tools\video-avatar\leia-keyed-packed.video-avatar.json" (Join-Path $dst2 "video-avatar.json")
```

Set **Video folder** to `$dst2` and press **Reload video**. If the outline holds with the packed clips and slips with the pairs, the cause is the two decoders. If it slips in both, it is in the clips or the shader. The packed path has not been run in a player.

## What the delivered clips are, measured

Measured from the files in `keyd\` on 2026-11-11. All four are 768×1344, 24 fps, 124 frames, 5.17 s, H.264, no audio, and each mask matches its picture in size, rate and length.

| Clip | What it shows | Jump from last frame to first, against a normal frame step |
| --- | --- | --- |
| `idle` | front, standing | 1.57 against 0.12: about 13 times a normal step. **The loop has a visible hitch.** |
| `idle_breeze` | front, hair moving | 1.56 against 0.26: about 6 times. Same hitch. |
| `walk` | faces left. Starts standing side-on, takes 5 steps, ends standing side-on | 1.66 against 2.37: smaller than a normal step. The loop point is a standing pose, so she pauses for a moment every 5 seconds. |
| `walk_stop` | faces left. Starts standing side-on, takes about 4 steps, **turns to the front and ends in the idle pose** | not a loop |

The numbers are the mean absolute difference of the masked picture, 0 to 255.

Clip-to-clip changes:

| Change | Jump | Reading |
| --- | --- | --- |
| `idle` last → `idle_breeze` first, and back | 1.58 | the same hitch as the idle's own loop |
| `walk` last → `walk_stop` first | 1.67 | fine: both are the side-on standing pose |
| `walk_stop` last → `idle` first | 1.67 | the same hitch as the idle's own loop |
| `idle` → `walk` first | 11.9 | **a hard cut from front to side.** There is no keyed turn clip. |

Consequences:

- **Only two idle clips were delivered**, `idle` and `idle_breeze`. That is the default idle and one variation, not three variations.
- **The walk-stop ends facing front**, not side-on. The delivered manifest therefore has `"after": "idle"`, and the default idle follows it. Remove that key to hold the walk-stop's last frame until **Up** is pressed.
- **A stop takes time.** The walk-stop starts from the side-on standing pose, which the walk only reaches at its loop point. So a stop waits for the walk loop to end (up to 5 s), then the walk-stop takes about 4 more steps (3.8 s) before she turns. A shorter walk loop of one stride, and a walk-stop that starts mid-stride, would fix this in the assets.
- **Walking speed** is an estimate: about 365 px per step at about one step per second gives 0.27 clip heights per second. Tune it with the slider.
- **Feet:** the clips have 23 to 31 px of empty space under the feet, so the manifest lowers each clip by 1.7% of its height.
- The source folder has unkeyed turn clips (`turn_around`, `walk_turn180`, `walk_pivot180`). A keyed turn would remove the hard cut at the start of a walk.

## Walking

Walking uses the existing desktop presence. The animation director's walk cues, and the test controls below, both call `IWalkRequest` on `DesktopPresence`. The 2D avatar watches the presence and picks the clip.

- **Start.** The walk clip starts at once and interrupts an idle or a variation.
- **Direction.** The clip faces left. Walking right draws it mirrored.
- **Speed.** While a walk clip is loaded, the presence moves at `speed × height of the clip on screen`, so she covers the same ground per step at any size. The speed follows the clip: zero before `moveStart`, full in between, zero after `moveEnd`. Without a walk clip the window moves at the 3D speed and the idle slides with it, as before.
- **Stop.** The presence keeps walking until the walk loop reaches its loop point, then through the steps of the walk-stop (`moveEnd`), and then stands. The walk-stop plays once.
- **After the stop.** A side idle if the folder has one. Otherwise the walk-stop's last frame is held, unless the manifest says `"after": "idle"`.
- **A new walk during the stop** cancels the stop in the presence and the walk clip starts again. The presence is the only thing that moves the window, so there is one direction at a time.
- **A screen edge, a drag or a fall** ends the walk at once. The walk-stop then plays where she is.

### Test controls

On the Avatar tab in 2D mode: **Test: walk left**, **Test: walk right (mirrored)**, **Test: stop**, **Test: back to the front idle**, and a **Walk speed** slider. The slider at its lowest uses the walk clip's own `speed`.

With the settings panel closed and the window focused (click her first):

| Key | Action |
| --- | --- |
| Left arrow | walk left until stopped, at most 30 s |
| Right arrow | walk right, mirrored |
| Down arrow | stop |
| Up arrow | back to the front idle from a held pose, a side idle or a variation |

The keys do nothing while a text field has focus.

## Keeping the picture and the mask together

- Each clip has its own pair of decoders. They are started in the same frame, paused together, and rewound together.
- **Loading.** A clip is shown only after both its picture and its mask report a frame from the current run. A leftover frame number from before a rewind does not count.
- **Switching.** The next clip is started first. The previous clip stays on screen until the next one has a picture of its own. Then the picture, the mask and the mirror flag change in the same frame. Nothing blank and no unmasked picture is drawn in between.
- **Playback and looping.** A mask one frame behind the picture plays 8% faster until it is in step, and one frame ahead plays 8% slower. That closes the gap in about half a second without a jump. A mask more than one frame away is moved onto the picture's frame, at most twice a second. At a loop point, a mask on the last frame and a picture on the first count as one frame apart.
- **Why one frame matters.** In the delivered walk, a mask one frame off changes about 16% of her outline area on average and up to 27%. The masks themselves are in step with their pictures in the files (measured), and hide about 0.3% of her. So a visible slip points at playback, not at the files. This is a measurement of the files, not of the player.
- **Sync log.** Every 15 seconds `Player.log` gets one line per playing pair, for example `VideoAvatar: sync walk frames=360 inStep=97% offByOne=3% offByMore=0% seeks=0`. A high `offByOne` or any `offByMore` while the outline slips confirms the two decoders as the cause. Each clip change logs `VideoAvatar: showing walk act=Walk mask=MaskVideo`.
- **Play-once clips** are paused one frame before their end, so the last picture stays on screen.
- **After use** a clip is rewound in the background, ready for next time. An idle variation is unloaded after it plays.
- A packed `.sbs.mp4` clip needs none of this, because one decoder produces both halves. Each half is read half a pixel inside its own edges, so the picture and the mask never blend at the middle of the file.

At most the default idle, the walk, the walk-stop, a side idle and one idle variation are loaded at a time.

## Sound

No clip's sound is ever played. Every decoder is created with `VideoAudioOutputMode.None`. The companion's voice is a separate path and is not changed.

## Size and position

On the Avatar tab, in 2D video mode:

| Control | Range | Meaning |
| --- | --- | --- |
| Video size | 20% to 100% | 100% is the largest size at which the default idle fits inside the window with its own aspect ratio. No clip is ever stretched. |
| Video left or right | −50% to 50% | Sideways shift as a share of the window width. |
| Video up or down | −30% to 30% | Vertical shift as a share of the window height. |
| Reset video size and position | | Back to 100%, centred, on the bottom edge. |

These apply to every clip. The default idle decides how large the character is. Every other clip is drawn at the same height times its own `scale`, with its own aspect ratio, centred on the same point, then moved by its own `offsetX` and `offsetY`. That is how clips with different framing keep one character size and one ground line.

To make her larger than the window allows, raise **Size** on the Overlay tab.

## How it works

- `VideoAvatar` is added to the `AppBootstrap` object by `SettingsPanel` at startup. The scene file is not changed. In 3D mode it does nothing.
- `VideoAvatarManifest` turns the folder listing and the manifest into a `VideoAvatarClipSet`.
- `VideoAvatarDirector` decides which clip should show. It is plain logic with no video types in it.
- `VideoClipPlayer` owns the decoders of one clip.
- `DesktopPresenceModel.SetGait` gives the presence a speed that can change every frame and a stop that waits. With no gait set, which is always the case in 3D mode, the model behaves exactly as before.
- The picture is a uGUI `RawImage` that is not a raycast target, on a canvas with no raycaster. Click-through, drag, double-click and the scroll wheel still depend on the alpha of the pixel under the cursor.
- `Resources/VideoAvatar.shader` combines the picture with the mask and writes premultiplied colour and alpha.
- While a clip shows, `AvatarMount.SetModelHidden(true)` stops the 3D model from being drawn. It stays loaded and animated.
- `ProjectSettings/GraphicsSettings.asset` has `m_VideoShadersIncludeMode: 2`, so a player build keeps the video decode shaders.
- Chat, voice, memory, private mode, the tray, screen reading and mood detection are not changed. No clip or frame is sent anywhere.

## Known behaviour

- She loops the idle while she speaks. There is no talking clip and no lip sync.
- Cues from the model still drive the hidden 3D face.
- **The expression portrait is hidden while the video shows.** It is placed beside the 3D model's head and drawn above the video, so in 2D mode it landed in the middle of her body. It comes back when the 3D model does: in 3D mode, and when the video cannot play and the app falls back to 3D. The setting itself is not changed.
- An AI walk cue of a few seconds lasts longer in 2D mode, because the stop waits for the walk loop and the walk-stop.
- **A walk cue needs the chat model to write it.** The path from a reply's `<animation gesture="walk" direction="left" />` to the walk clip is the same in both modes and has a test. In the player logs from 2026-11-11 the local model (Rocinante-X-12B) wrote no `<animation>` tag in any reply: every reply logs `Cue emotion: (none)` and `AnimationDirector: no cue -> neutral`. Then nothing can walk, in 3D or 2D. When a cue does arrive, the log has `Walk: request received dir=-1 secs=12` and `AnimationDirector: cue walk -> requested`.
- At a screen edge the walk-stop plays its steps on the spot.
- Rotate, zoom and Ctrl+scroll still act on the hidden 3D model.
- While she only stands, the overlay drops to 15 fps after a few seconds without the pointer, and the idle is shown at that rate. Variations, walking and clip changes run at the full rate.

## Build and test in PowerShell

These commands use a second working folder, so your existing clone and its uncommitted Unity files are not touched. They do not reset, clean or stash anything. They were written from `docs/TESTING.md` and Unity's command line arguments and **have not been run by the author**.

```powershell
$unity = "E:\Unity Editor\6000.6.6f1\Editor\Unity.exe"
$repo  = "C:\path\to\ai-companion"          # your existing clone. Left as it is.
$test  = "C:\path\to\ai-companion-pr56"     # a new folder for this test

# 1. Check out PR #56 next to your clone, without changing its branch or files.
git -C $repo fetch origin claude/juh-99-video-avatar
git -C $repo worktree add --detach $test origin/claude/juh-99-video-avatar

# 2. EditMode tests. The first run imports the project and takes several minutes.
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList @(
    "-batchmode", "-nographics", "-projectPath", "`"$test`"",
    "-runTests", "-testPlatform", "EditMode",
    "-testResults", "`"$env:TEMP\leia-editmode.xml`"",
    "-logFile", "`"$env:TEMP\leia-editmode.log`"")
"Unity exit code: $($p.ExitCode)"
[xml]$x = Get-Content "$env:TEMP\leia-editmode.xml"
$x.'test-run' | Select-Object total, passed, failed
$x.SelectNodes("//test-case[@result='Failed']") | Select-Object fullname

# 3. Windows player.
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList @(
    "-batchmode", "-quit", "-projectPath", "`"$test`"",
    "-buildWindows64Player", "`"$test\Builds\Windows\BackseatCatgirl.exe`"",
    "-logFile", "`"$env:TEMP\leia-build.log`"")
"Unity exit code: $($p.ExitCode)"
Test-Path "$test\Builds\Windows\BackseatCatgirl.exe"

# 4. Run it.
& "$test\Builds\Windows\BackseatCatgirl.exe"
```

Notes:

- `docs/TESTING.md` says the editor exits with code 2 when tests ran. Read the XML for the result.
- One known failure is unrelated: `SpeechToTextTests.NativeLoad_ListsEditorAndPlayerPluginFolders`.
- `Assets/LocalAvatars` is not in Git, so the new folder has no local 3D model. The 3D avatar there is the sample VRM or the capsule. Copy `Assets\LocalAvatars` and `Assets\LocalAvatars.meta` from your clone if you want your model in the test build.
- The player reads the same `%APPDATA%\ProjectLeia\settings.json` as your normal build. New keys are added to it. No existing key is changed.
- To remove the test afterwards: `git -C $repo worktree remove $test`. Add `--force` only if you are sure nothing in that folder is needed.

## Windows checklist

Mark each line. Lines 1 to 6 repeat what was verified before, on the new code.

**Fallback and switching**

1. In 3D mode the app looks and behaves as before.
2. **Avatar: 2D video** with the test folder shows her. The Avatar tab reads `Playing idle.mp4 ... Idle variations: 1. Walk: walk. Walk-stop: walk_stop. Clips are listed in video-avatar.json.`
3. **Avatar: 3D model** shows the 3D avatar at once. Switch back and forth five times.
4. Point **Video folder** at an empty folder. The 3D avatar shows and the tab names the problem. Point it back.
5. Rename `walk.mask.mp4`, press **Reload video**. She still idles. The tab says the walk clip was left out. Left arrow slides the idle. Rename it back and reload.
6. Rename `idle.mp4`, reload. The 3D avatar shows. Rename it back and reload.

**Idle**

7. The desktop shows around her. No green and no rectangle. No dark or light fringe.
8. Watch three idle loops. Note the hitch at the loop point (expected from the source).
9. Wait up to a minute. The breeze variation plays once and the idle returns. The change happens at the loop point, not mid-loop, with no flash of green and no blank frame.
10. No sound from any clip. The companion's voice still plays.

**Walking**

11. Click her, press **Left**. She cuts to the side pose and walks left. The window moves.
12. Watch her feet against the taskbar. A planted foot should stay on one spot. If it slides backwards the speed is too low, forwards too high. Tune **Walk speed** and note the value that fits.
13. At the walk's loop point (every 5 s) she stands for a moment. The window should slow down and stop with her, then start again.
14. Press **Right**. She walks right, mirrored. Edges and transparency are still clean.
15. Press **Down**. She keeps walking to the end of the loop, then the walk-stop plays once: a few steps, a turn to the front. The window stops by the time she turns. Then the idle continues.
16. During steps 11 to 15: no green flash, no blank frame, no frame where the outline does not fit her.
17. Press **Left**, then **Down**, and press **Left** again while the walk-stop plays. She walks again. No freeze and no blank frame. The window does not jump or move both ways.
18. Press **Left** and **Right** quickly several times, then **Down**. She ends standing.
19. Walk into the left screen edge. The window stops. The walk-stop plays on the spot (known).
20. Drag her while she walks. Let go. She falls to the taskbar and ends standing.
21. Remove `"after": "idle"` from `video-avatar.json`, reload, walk and stop. The last frame is held. **Up** returns to the idle.

**Size and alignment**

22. Her size on screen is the same in idle, walk and walk-stop.
23. Her feet are on the taskbar line in all clips. If not, change `offsetY` in the manifest and reload.
24. The size and position sliders work in every clip and survive a restart.

**Everything else**

25. Click-through on the empty part, drag on her, double-click for chat, in idle and while walking.
26. Chat, voice, push-to-talk, private mode, tray pause and the settings panel behave as before in both modes.
27. Ask the model something that makes her walk. She walks and stops.
28. Leave it running for 10 minutes. Check that the outline still fits and memory use is steady.
29. Repeat 7 to 18 with the packed `.sbs.mp4` clips (see [Packed clips](#packed-clips-the-test-for-the-slipping-outline)). Compare the outline with the pairs.
30. Portrait: turn **Expression portrait** on in 3D mode. Switch to 2D: the portrait is gone. Walk left and right, stop: still gone. Switch to 3D: it is back beside her head. Repeat the switch twice.
31. Portrait fallback: in 2D mode set **Video folder** to an empty folder and press **Reload video**. The 3D model and the portrait both show.
32. After a walk test, open `Player.log` and find the `VideoAvatar: sync` lines. Note `offByOne` and `offByMore` for the walk.

## Tests

- `VideoAvatarDirectorTests`: the packed clip preferred over the pair, the mask offset across the loop point and the speed nudge, the packed halves' edges, the portrait rule through every mode switch and fallback, clip lookup by file name with the delivered names, the manifest, missing clips and masks, variations only at the loop point, walk and stop, interruptions, a new walk during the stop, clips that failed to load, speed that follows the clip, the frame rules for a fresh picture and for the loop point, and the layout for a set of clips.
- `DesktopPresenceGaitTests`: the gait on the presence, and that nothing changes without it.
- `WalkRequestWiringTests.ReplyWalk_In2DVideoMode_...`: a reply's walk cue moves the presence at the clip's speed and starts the walk clip, and a stop cue goes through the walk-stop.
- `VideoAvatarTests`: the earlier tests for modes, settings, the single-clip lookup, the layout, the mask sync rule, the shader and the build setting.

No test decodes video. That part is the checklist above.
