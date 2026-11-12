# 2D video avatar

An optional avatar mode that shows full-body video clips in the overlay window in place of the 3D model (JUH-99). The 3D avatar is the default and stays loaded. This mode only hides it.

What plays:

- a default idle loop
- optional idle variations, started only when the idle reaches its loop point
- a walk loop while the desktop presence moves the window, mirrored for the other direction
- a walk-stop, played once when the walk ends

Reaction clips (`happy`, `concerned`, `turn360`, `embarrassed`, `flustered`, `pout`, `wink_dance`) play once from the default idle and return to it. `turn_around_back` is followed by the held back pose `back_idle`. See [Reactions](#reactions). `walk_pivot180` is registered and not played yet.

## What is verified and what is not

| Area | Status |
| --- | --- |
| Idle playback with a mask clip in a Windows build | Verified by Jussi on 2026-11-11, on the first version of this code |
| Clip lookup, manifest, transitions, interruptions, gait, layout, settings | Automated EditMode tests |
| C# compiles for the Windows player | CI job "Windows player script compile" |
| Idle variations, walking, walk-stop, mirroring and clip switching with the picture + mask pairs | Jussi, manual test of commit 905d671 on Windows: "work pretty well". Two faults were reported and are addressed after that commit, see the next rows |
| Expression portrait beside her head in 2D mode, steady while she walks | Automated tests of the anchor, the position and every mode switch. **Not yet seen in a player** |
| Outline that slips (mask clipping) | Jussi, Windows build of e4e5657, 2026-11-12, with the reliable sync log. **Pairs drift:** walk runs 80.0%, 82.1%, 94.7% in step, with 14.8% to 17.2% of frames one frame off and 2.8% to 3.1% more than one; one idle run 89.2% in step, other idle runs 99.6 to 100%; walk-stop and the reactions 100%. **Packed: one decoder, so no drift is possible.** Every run was counted, with at most 1 frame skipped. The speed nudge did not keep the walk pair in step. Use the packed clips |
| A walk cue from the chat model in 2D mode | Automated test from the reply text to the walk clip. **Not seen working in a player:** the local model wrote no cue tag (see the next row). |
| Reaction clips `happy`, `concerned`, `turn360` | Jussi, Windows build of e4e5657: played once and returned to the idle (sync log). |
| Reaction clips `embarrassed`, `flustered`, `pout`, `wink_dance`, and `turn_around_back` → `back_idle`; `idle_breeze` variations | Jussi, Windows build of 1d5d498, 2026-11-12, packed clips: all 13 clips play, `decoders=1`, at most 1 frame skipped; every test button works; variations play on their own. |
| AI-triggered reactions | **Not working in the player on 1d5d498:** every reply logged `Cue emotion: (none)` and `AnimationDirector: no cue -> neutral`; the local model (Rocinante-X-12B) wrote no cue tag. Changed after that: the tag goes first, a reminder on each turn, a more tolerant parser, the `reaction` attribute, the reaction list in the prompt, the raw tag in `Player.log`, and a short wait for a busy avatar. Automated tests only. **Not run in a player.** |
| Clip sound | Automated tests of the rules and the manifest field. The audio tool's ffmpeg commands were run on the six voice clips (video stream bit-identical, all frames kept). **Not heard in a player.** |
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
| anything else, for example `happy` | reaction | once |

The mask for `name.mp4` is `name.mask.mp4`, `name_mask.mp4` or `name-mask.mp4` (also `.png` or `.jpg` for a still mask). White shows her, black is see-through.

`name.sbs.mp4` is a packed clip: picture in the left half, mask in the right half. If the folder has both `name.sbs.mp4` and `name.mp4`, the packed clip is used, because one decoder produces the picture and its mask in the same frame. A manifest can still name the pair.

A clip with no mask and no packed form uses its own alpha channel. A clip with no alpha is fully opaque.

If there is no `idle` but there are idle variations, the first variation in alphabetical order becomes the default idle and the Avatar tab says so.

### With a manifest: `video-avatar.json`

Put a file named `video-avatar.json` in the clip folder to list the clips yourself. Then only the listed clips are used. File names in it are names inside the clip folder. A path is cut down to its file name.

`tools/video-avatar/leia-keyed.video-avatar.json` is the ready manifest for the delivered keyed clips, in the **packed** form (`name.sbs.mp4`). Packed is the default since the Windows sync measurement on 2026-11-12: one decoder per clip, so the outline cannot slip. `tools/video-avatar/leia-keyed-pairs.video-avatar.json` lists the same clips as picture + mask pairs, for comparison.

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
| `hold` | A pose that follows a reaction. Seconds she holds it before facing front again, at a loop point. | `12` |
| `after` | On a reaction: the name of a looping reaction clip played after it as a held pose, for example `"turn_around_back"` → `"back_idle"`. On the walk-stop: `idle` if the clip ends facing front in the idle pose, so the default idle may follow. Leave it out to hold the last frame. | hold |
| `sound` | `true` plays the clip's own sound track, with **Play clip sounds** on. See [Sound](#sound). | `false` |
| `weight` | Idle variation only. How often it is picked against the others. | `1` |
| `variationMinSeconds`, `variationMaxSeconds` | Top level. Time on the default idle before a variation is queued. | `12`, `30` |
| `headX`, `headY` | Top level. Where her head is in the default idle's frame, as shares from its left and its top. The expression portrait sits beside it in 2D mode. | `0.5`, `0.15` |

To add a reaction later, add a clip with `"role": "reaction"` and a name such as `happy`, `concerned`, `surprised` or `annoyed`. `VideoAvatarClipSet.FindReaction(name)` finds it. Playing it is a later change.

After editing the manifest or the folder, press **Reload video** on the Avatar tab.

## Install the delivered clips for a test

This copies files into a new folder and changes one setting. Nothing in your source folder is changed. To undo it, clear **Video folder** or choose **Avatar: 3D model**.

```powershell
$src   = "C:\Users\Jussi\Downloads\portraits\animations\keyd"
$dst   = Join-Path $env:APPDATA "ProjectLeia\video-avatar-packed"
$repo  = "C:\path\to\ai-companion-pr56"     # the PR #56 checkout, see below
$clips = "idle", "idle_breeze", "walk", "walk_stop", "happy", "concerned", "turn360",
         "embarrassed", "flustered", "pout", "wink_dance", "turn_around_back", "back_idle"

New-Item -ItemType Directory -Force $dst | Out-Null
foreach ($name in $clips) { Copy-Item "$src\$name.sbs.mp4" $dst }
Copy-Item "$repo\tools\video-avatar\leia-keyed.video-avatar.json" (Join-Path $dst "video-avatar.json")
Get-ChildItem $dst | Select-Object Name, Length
```

Then, in the app: **F12** → **Avatar** → **Avatar: 2D video**, set **Video folder** to the path printed by `$dst`, and press **Reload video**.

Expected files in the folder: a `.sbs.mp4` for each name in `$clips`, and `video-avatar.json`. Copying again over an earlier folder replaces its `video-avatar.json` with the new one, which is what adds new clips.

### Picture + mask pairs, for comparison

The pairs use two decoders per clip, which drift apart on the walk (see [What is verified](#what-is-verified-and-what-is-not)). Only for comparison:

```powershell
$dst2 = Join-Path $env:APPDATA "ProjectLeia\video-avatar-test"
New-Item -ItemType Directory -Force $dst2 | Out-Null
foreach ($name in $clips) { Copy-Item "$src\$name.mp4", "$src\$name.mask.mp4" $dst2 }
Copy-Item "$repo\tools\video-avatar\leia-keyed-pairs.video-avatar.json" (Join-Path $dst2 "video-avatar.json")
```

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

## Screen edges

A walk toward the screen edge she stands at ends at once. A walk away from it starts normally. Before the fix on this branch, a walk away from an edge also ended at once in 2D mode, because she stands still for a moment while the walk clip starts. After she had walked to an edge, every walk in either direction went straight to the walk-stop. `Player.log` now says `Walk: ended at the left screen edge` when an edge ends a walk.

## Reactions

A reaction is a play-once clip with the role `reaction`. Without a manifest, any clip whose name is not an idle, a walk or a stop is a reaction.

- **Asked for by the reply.** The cue tag can name a reaction: `<animation emotion="sad" reaction="pout" />`. While the 2D avatar plays, the system prompt lists the reactions in the loaded folder, so a new clip is offered as soon as it is in the folder and the manifest. The list leaves out a pose held after another reaction (`back_idle`) and walk turns (`walk_pivot180`). A model that writes the reaction as the emotion (`emotion="pout"`) gets it too, when that word is a listed reaction. So does one that writes it as the gesture (`gesture="pout"`, `gesture="wink_dance"`), which is what Rocinante-X-12B did in the 2026-11-12 player test: before this, only its walk played. A word that is the start of exactly one listed name counts as that name (`turn_around` → `turn_around_back`, `wink` → `wink_dance`). `walk` and `stop` are never reactions.
- **From an emotion.** When the avatar's emotion changes to one that has a clip of the same name: `happy`, `sad`, `surprised`, `thinking`, `embarrassed`, `shy`, `flustered`, `concerned`. The emotion is the one the 3D face already uses. The same emotion twice in a row plays once. A reaction the reply named always wins over one from an emotion change.
- **Test buttons.** The Avatar tab has a **Test** button for each delivered reaction.
- **From where.** It starts from the default idle. Asked for while she walks, stops, holds a pose, or plays a variation or another reaction, it waits up to 5 seconds and plays when she is back on the default idle. A newer request replaces a waiting one. After 5 seconds it is dropped.
- **In Player.log.** Every reply logs the tag the model wrote: `Cue tag: <animation emotion="sad" reaction="pout" />`, `Cue tag (not readable, used neutral): ...`, or `Cue tag: none found in the reply`. A reaction logs `AnimationDirector: reaction pout -> started`, `-> waiting`, `-> refused`, or `-> no reaction avatar (2D video mode is off)`.
- **How.** The clip is loaded when it is asked for, starts as soon as it has a picture, without waiting for the idle's loop point, and is unloaded after. A walk interrupts it.
- **The delivered clips, measured.** `happy`, `concerned` and `turn360` are 124 frames at 24 fps, start in the idle's first pose and end close to it. The cut from any idle frame into them is 0.6 to 2.6 on the scale where a normal idle frame step is 0.14 and the idle's own loop jump is 1.6. So expect a small jump at the start and at the end, about as large as the idle's loop jump. **Not seen in a player.**
- **New on 2026-11-12, measured on the packed files.** `embarrassed`, `flustered`, `pout` (124 frames) and `wink_dance` (243 frames, 10 s; she turns around mid-dance) start and end close to the idle pose. The jump into them and back is 0.4 to 2.3 on the same scale, about the idle's own loop jump.
- **A held pose after a reaction: `turn_around_back` → `back_idle`.** `turn_around_back` (158 frames) starts in the idle pose and ends with her back turned, looking over her shoulder. `back_idle` loops that pose: its start matches the turn's end (0.78) and it loops almost cleanly (0.42). The manifest plays the turn, then holds `back_idle` for `hold` seconds (12), then she faces front at a loop point. **There is no clip that turns her back to the front, so facing front is a hard cut** (11 on the same scale, like the cut from the idle into the walk). A walk, the Up arrow or **Test: back to the front idle** ends the pose at once, also with that cut. Without a manifest `turn_around_back` returns straight to the idle, with the same cut, and `back_idle` is a plain reaction. A keyed turn-to-front clip would remove the cut.
- **Not used yet: `walk_pivot180`.** It starts in the walk's first pose facing left and ends in the mirrored walk pose facing right: a change of direction during a walk. Using it needs the window to hold its direction until the pivot ends, which is not built. Today a change of direction flips the walk clip at once. By file name it is registered as a reaction, so it is not mistaken for a second walk loop.
- There is no emotion detection from the reply's words and no lasting mood state (that is JUH-103). If the chat model writes no `<animation>` tag, no reaction plays, as with walking.

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
| Up arrow | back to the front idle from a held pose (the side pose or the back pose), a side idle or a variation |

The keys do nothing while a text field has focus.

## Keeping the picture and the mask together

- Each clip has its own pair of decoders. They are started in the same frame, paused together, and rewound together.
- **Loading.** A clip is shown only after both its picture and its mask report a frame from the current run. A leftover frame number from before a rewind does not count.
- **Switching.** The next clip is started first. The previous clip stays on screen until the next one has a picture of its own. Then the picture, the mask and the mirror flag change in the same frame. Nothing blank and no unmasked picture is drawn in between.
- **Playback and looping.** A mask one frame behind the picture plays 8% faster until it is in step, and one frame ahead plays 8% slower. That closes the gap in about half a second without a jump. A mask more than one frame away is moved onto the picture's frame, at most twice a second. At a loop point, a mask on the last frame and a picture on the first count as one frame apart.
- **Why one frame matters.** In the delivered walk, a mask one frame off changes about 16% of her outline area on average and up to 27%. The masks themselves are in step with their pictures in the files (measured), and hide about 0.3% of her. So a visible slip points at playback, not at the files. This is a measurement of the files, not of the player.
- **Sync log.** Each clip's run ends with one line, and a long run also gets one every 15 seconds:
  - pair: `VideoAvatar: sync walk file=walk.mp4+walk.mask.mp4 decoders=2 frames=96 inStep=97.9% offByOne=2.1% offByMore=0.0% skipped=3 seeks=0`
  - packed: `VideoAvatar: sync idle file=idle.sbs.mp4 decoders=1 frames=310 skipped=2 (one decoder: the mask is always on the picture's frame)`
  - `frames` counts picture frames of the clip, each once. `skipped` counts frames that could not be measured: a rewind or a resync seek was in flight, or the run had not shown its first frame yet. Those are not drift. `seeks` counts the jumps made to bring the mask back.
  - Each clip change logs `VideoAvatar: showing walk act=Walk mask=Packed file=walk.sbs.mp4`, and each load logs the folder and whether `video-avatar.json` was used.
  - **Logs from builds before this change are not reliable.** They counted app frames, not clip frames, counted a rewind in flight as drift, and wrote nothing for packed clips.
- **A lead on the pairs slipping (not confirmed).** All the clips are H.264 High profile with B-frames. Windows Media Foundation warns about these files when loading ("not encoded with the baseline profile", and for `walk.mask.mp4` "First video frame not zero: 4"). The files themselves start at 0 s (checked with ffprobe), so this is the decoder. Two decoders starting a few frames apart would slip the outline, and a packed clip cannot. A test: re-encode a pair without B-frames into a new folder, for example `ffmpeg -i walk.mask.mp4 -c:v libx264 -bf 0 -crf 16 -pix_fmt yuv420p out\walk.mask.mp4`, and compare its sync lines. Your original files stay as they are.
- **Play-once clips** are paused one frame before their end, so the last picture stays on screen.
- **After use** a clip is rewound in the background, ready for next time. An idle variation is unloaded after it plays.
- A packed `.sbs.mp4` clip needs none of this, because one decoder produces both halves. Each half is read half a pixel inside its own edges, so the picture and the mask never blend at the middle of the file.

At most the default idle, the walk, the walk-stop, a side idle and one idle variation are loaded at a time.

## Sound

Some original Comfy clips have her voice and sounds. A clip plays its sound only when all of these hold:

- the manifest marks it `"sound": true` (without a manifest, no clip has sound),
- **Play clip sounds** is on in the Avatar tab (off by default), with **Clip sound volume** 0 to 1,
- speech is not muted (the Mute button and F6 silence clip sound too),
- she is not speaking. While her TTS voice plays, the clip runs on silently. A reaction from a reply usually plays while she still speaks that reply, so its sound is often skipped.

Only the clip on screen sounds. A clip with sound logs `VideoAvatar: clip sound pout: on`, `silent while she speaks`, `silent (speech is muted)` or `off (Play clip sounds is off)` when it shows.

### The packed clips have no sound: adding it

Keying wrote the `.sbs.mp4` files without an audio track (checked with ffprobe: video only, all 14). The original Comfy clips have one: stereo AAC, 32 kHz, the same length as the video. The six "voice" sources have real sound (mean −19 to −28 dB): `embarrassed`, `flustered`, `pout`, `wink_dance`, `turn_around_back`, `back_idle`. The other sources are silent (−91 dB), except `walk`, which has a loud track of unknown content, left off.

`tools/video-avatar/add-clip-audio.ps1` finds each clip's source through its `NAME.key.json` and writes a new `NAME.sbs.mp4` with the packed video copied unchanged and the source's audio track added. It needs ffmpeg on PATH (`winget install ffmpeg`). Nothing in `keyd` or the source folders is changed, and it refuses to write into `keyd`.

```powershell
$repo = "E:\LOOT\ai-companion-pr56"
$keyd = "C:\Users\Jussi\Downloads\portraits\animations\keyd"
$withAudio = "C:\Users\Jussi\Downloads\portraits\animations\keyd-audio"
& "$repo\tools\video-avatar\add-clip-audio.ps1" -KeydFolder $keyd -OutFolder $withAudio
# Then copy the six over the same names in the video folder:
Copy-Item "$withAudio\*.sbs.mp4" (Join-Path $env:APPDATA "ProjectLeia\video-avatar-packed") -Force
```

By default it takes every clip whose source name contains `_voice_`. `-Names pout, wink_dance` picks clips, `-Force` replaces files already in the output folder. Checked here on the six voice clips with the same ffmpeg commands: video stream bit-identical, frame counts kept (124, 158 and 243). The shipped manifest already marks those six `"sound": true`.

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
- **The expression portrait shows in both modes, beside the head of the avatar on screen.** In 3D mode it follows the 3D model's head bone, as before. In 2D mode it sits beside the video avatar's head, taken from the default idle's frame (centre, 15% down from the top, measured on the delivered idle). That point does not change between clips, so the portrait stays in one place in the window while she walks, stops or reacts, and moves only with the window and the size and position settings. Before this, 2D mode followed the hidden 3D model's head bone, which keeps animating behind the video: once she walked, the portrait slid onto her chest and stayed there. A manifest can set `"headX"` and `"headY"` (shares of the idle frame from its left and its top) for clips framed differently.
- An AI walk cue of a few seconds lasts longer in 2D mode, because the stop waits for the walk loop and the walk-stop.
- **Walks and reactions need the chat model to write the cue tag.** The path from a reply's tag to the walk clip or a reaction is the same for every model and has tests. In the player logs from 2026-11-11 and 2026-11-12 the local model (Rocinante-X-12B) wrote no tag in any reply. It wrote roleplay narration (*her cheeks flush*) instead. Three things worked against it: the tag was asked for at the end of the reply, after the narration; the chat history the model sees never shows a tag, because tags are taken out of the transcript on purpose (so it does not copy the last emotion); and a tag without the closing slash, with unquoted values, or with an unknown emotion word was thrown away. Now the instruction asks for the tag first, each request adds a one-line reminder to the newest user message (the model sees it, the transcript and memory do not), and the parser reads those sloppy forms. Whether this model follows it is **not yet seen in a player**. `Cue tag:` lines in Player.log show what arrives.
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
30. Portrait: turn **Expression portrait** on in 3D mode. Switch to 2D: the portrait is beside her head. Walk left and right, stop, play a reaction: it stays in the same place in the window and never moves onto her body. Switch to 3D: it is beside the 3D head. Repeat the switch twice.
31. Portrait fallback: in 2D mode set **Video folder** to an empty folder and press **Reload video**. The 3D model shows with its portrait beside its head.
32. Reactions: in the front idle press **Test: happy**, **Test: concerned**, **Test: spin (turn360)**, one at a time. Each plays once and returns to the idle. Note the size of the jump at the start and the end. Press one while she walks: nothing happens.
33. After a walk test, open `Player.log` and find the `VideoAvatar: sync` lines. Check that each names the file you expect. For pairs note `offByOne`, `offByMore` and `seeks` for the idle and the walk.
34. Chat: ask her to pout, to dance, to look away, and say something that should fluster her. For each reply, find `Cue tag:` and `AnimationDirector: reaction` in Player.log. A reaction should play from the front idle, or wait for it if she was walking.
35. Clip sound: install the audio clips (see [Sound](#sound)), turn **Play clip sounds** on, press **Test: pout** and **Test: wink dance**. Her sound plays with the clip. Press **Mute**: silent. Unmute, and play one while she speaks a reply: silent until she stops, the clip goes on.

## Tests

- `VideoAvatarDirectorTests`: reactions (start from the idle when loaded, return to the idle, refused elsewhere, interrupted by a walk, failed clips, names per emotion, the pivot not taken for a walk), the packed clip preferred over the pair, the mask offset across the loop point and the speed nudge, the packed halves' edges, the portrait rule through every mode switch and fallback, clip lookup by file name with the delivered names, the manifest, missing clips and masks, variations only at the loop point, walk and stop, interruptions, a new walk during the stop, clips that failed to load, speed that follows the clip, the frame rules for a fresh picture and for the loop point, and the layout for a set of clips.
- `VideoAvatarDirectorTests` (2026-11-12): a reaction asked for while she walks, plays a variation or another reaction waits and then plays; one waiting too long is dropped; the latest wins; an emotion reaction never replaces a named one; held poses and unknown clips are refused; the offered list; the `sound` field; the clip sound rules; the two settings survive a save and load; the shipped manifest is packed and marks the six voice clips.
- `CueReactionTests`: the tag as models write it (case, spacing, attribute order, single or no quotes, no closing slash, at the start, in the middle, at the end, a loose `<reaction=.../>`), a reaction kept with an unknown emotion, a broken second tag, the raw tag for the log, the stream filter with the tag first, the reaction list and the prompt (the instruction is sent even without a gesture catalog), and which reaction a cue asks for.
- `CueTranscriptTests`: the turn reminder reaches the model and nothing else; the raw tag and "none found" for the log.
- `DesktopPresenceGaitTests`: the gait on the presence, and that nothing changes without it.
- `WalkRequestWiringTests.ReplyWalk_In2DVideoMode_...`: a reply's walk cue moves the presence at the clip's speed and starts the walk clip, and a stop cue goes through the walk-stop.
- `VideoAvatarTests`: the earlier tests for modes, settings, the single-clip lookup, the layout, the mask sync rule, the shader and the build setting.

No test decodes video. That part is the checklist above.
