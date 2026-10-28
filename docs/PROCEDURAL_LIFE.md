# Procedural life

`ProceduralLife` on `AppBootstrap` keeps the avatar moving between clips. It does not replace `Companion.controller`, and it does not rename `Idle` or `Walk`. Those clip names stay stable so a local avatar can still override them.

The motions are additive. Blink weight is combined in `AvatarAnimationController` at display time. Breath and idle sway are small rotations in the avatar's body axes, so they stay on her when she is yawed. Cursor look uses the camera's up and right, so screen-up stays up. Both run in `LateUpdate` after the Animator.

## Blink

Intervals are random, about 1.8 to 6.5 seconds, with an occasional longer gap up to 10 seconds. About one blink in five is a double blink. The close is shorter than the open (0.07 s close, a short hold, 0.11 s open).

Shape names are matched flexibly: `Blink`, `eyeBlink_L` / `eyeBlink_R`, `EyeClose`, VRM `blink`, and VRoid `Fcl_EYE_Close`. A VRM expression that already maps to the blink preset is included even when the mesh name is custom. The Console logs one line per avatar:

```text
ProceduralLife: blink blendshape Blink
ProceduralLife: blink blendshapes eyeBlinkLeft, eyeBlinkRight
ProceduralLife: no blink blendshape, blinking skipped
```

A missing shape does not error. The blink is `max(emotion blink, procedural blink)`, so Thinking can keep its slight squint between blinks. Mouth, brow, and smile channels are not written. Lip sync still owns the jaw.

## Breath and idle

Breath is a chest (or spine) pitch of about one degree, around thirteen breaths a minute. It starts at zero so the first frame does not pop off the clip.

There is one idle clip in the repo, so the variations are procedural: center, weight to either side, a head tilt either way, and a small look to either side. Holds last 4.5 to 9 seconds. The next variation is never the current one. A 1.1 second smoothstep crossfade joins them, and a long frame cannot skip that fade.

## Cursor look

Eyes and head follow the desktop cursor. On the Windows player this is the global cursor (`GetCursorPos` through the existing window chrome), so it still updates when the overlay does not have focus. The editor and non-Windows players use the game-view pointer.

Head yaw stays within 20 degrees and pitch within 12. Eyes lead a little and stop at 28 / 16. Past about 46 degrees of yaw or 34 of pitch (the cursor is far from her head on screen) she looks forward again. A cursor that has not moved for 2.5 seconds does the same. Both eases are smoothed.

**Settings > Avatar > Look at cursor** saves `cursorLookAt` in `%APPDATA%\ProjectLeia\settings.json`. The default is on. Older settings files without the key stay on.

## Gestures and walk

Sway and look fade out over about 0.28 seconds while the Animator layer named `Gestures` is in `Wave`, `Nod`, `Shrug`, `Laugh`, `Walk`, or `Turn180`, including the transition into one of those states, and while the `Walking` bool is set. Blink keeps running. If that layer is not on the controller yet, the sway stays on and nothing else changes. The controller asset is not modified by this feature.

## Tests

`ProceduralLifeTests` covers blink interval bounds, the close/open curve, double blinks, blendshape name selection, blink versus emotion and mouth channels, look-at clamping and the return to neutral, idle crossfades without a repeated pose or a pop, gesture suppression, and the settings round-trip. Avatar rendering is still a manual check on the Windows player.
