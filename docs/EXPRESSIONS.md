# Expressions

The face is data. `Assets/_Project/Resources/ExpressionPresets.json` lists a weight for each channel of each emotion. `EmotionFace.EmotionPose` reads that file (with the same text as a fallback through `Resources`) and scales every weight by the cue intensity. A channel or a blendshape that the avatar does not have is skipped.

`Companion.controller`, the Gestures layer, and the clip names are unchanged. Embarrassed, shy, flustered, and concerned are face-only: the animator Emotion int stays `0` (Idle) so the body is not left in the previous clip. Happy, sad, surprised, and thinking still use ints `1` to `4`.

## Sheet

The drawn sheet is the target. It is not in the repo.

| Drawing | Emotion | What the weights do |
| --- | --- | --- |
| Laughing, open mouth | `happy` at a high intensity, and the `laugh` gesture (happy at 0.9) | Smile (`joy`), smile-eyes (`eyeSquint`), jaw open (`aa`) |
| Gentle smile | `happy` at a lower intensity, or emotion `smile` | Same channels, scaled down, so the mouth barely opens |
| Concerned | `concerned` (`worried` is the same) | Inner brows up. Eyes stay narrow. A VRM that only has the whole `Surprised` shape gets a little of that instead |
| Strong blush, eyes down | `embarrassed` | Blush at 1, gaze and lids lowered, a little inner brow for the worried blush |
| Neutral | `neutral` | Everything at 0 |
| Light blush, small smile | `shy` | Blush about 0.38, lowered eyes, a small smile |
| Blush and an open mouth | `flustered` | Blush near 1 and an open jaw |
| Embarrassed and worried | `embarrassed` | Strong blush plus the inner brow. `concerned` is the less-blushed worry |

Sad, surprised, and thinking keep their previous mouth and brow balance, and surprised also raises inner brows, outer brows, and widens the eyes on avatars that have those shapes separately.

## Names

`ExpressionPresetMap` matches VRM presets (`Joy`, `Fcl_ALL_Joy`), ARKit (`mouthSmileLeft`, `browInnerUp`, `eyeSquintLeft`, `eyeLookDownLeft`), and Daz-style names (`eCTRLMouthSmile`, `eCTRLBrowsInnerUp`, `eCTRLEyeSquintL`, `eCTRLBlush`). Anything else, including `tongueOut`, is ignored. Blink still uses only blink and eye-close names, so a look-down shape does not blink.

While she is talking, lip sync replaces `aa`, `ih`, `ou`, `ee`, and `oh`. Smile, brows, squint, look-down, and blush stay. A procedural blink takes the max of the emotion lids and the blink, and does not write the mouth.

## Blush

`BlushSettings.Enabled` is the global switch (`true`). Set it to `false` and both the shape and the overlay stay off.

If the mesh has a blush shape (`blush`, `cheekBlush`, `eCTRLBlush`, `tere`, `照れ`), that shape gets the weight. Otherwise two soft quads parent to the head bone and use `AICompanion/CheekBlush`, a URP transparent shader. The texture is built in `BlushTexture`: a pink disk and faint diagonal hatch, original to this project. The quads sit at `BlushPlacement.LeftLocal` and `RightLocal` in head-local space (+Z toward the face). One Console line says which path ran:

```text
Blush: blendshape
Blush: procedural overlay
Blush: no head bone, overlay skipped
Blush: off
```

## Prompt

`AnimationDirector.SystemPromptSuffix` is the only prompt change. It still appends the catalog and `AnimationCueParser.Instruction`, then one line of allowed emotion values. The catalog rows `embarrassed`, `shy`, `flustered`, and `concerned` have no gesture and no animator state. ChatPanel, the provider, and `Scripts/Memory` are untouched.

```text
<animation emotion="embarrassed" intensity="0.9" />
```

## Tests

`ExpressionPresetTests` covers preset lookup, unknown channels, VRM / ARKit / Daz names, blush strength, lip-sync priority over the mouth, and the blink leaving blush alone. Rendering the overlay is a manual check.

## Local check

Build the Windows player and talk to her.

1. Keys **1**–**5** still select neutral, happy, sad, surprised, and thinking. The body clips are the same.
2. A reply whose cue is `emotion="embarrassed"` lowers her eyes and brings in a strong blush. `shy` is a lighter blush and a small smile. `flustered` blushes and opens the mouth. `concerned` or `worried` raises the inner brows without that strong blush.
3. `happy` near 1, or the laugh gesture, smiles with the eyes and opens the mouth. A lower intensity is the gentler smile.
4. While audio is playing, the mouth follows the words. Brows, smile, and blush stay on the emotion. She still blinks.
5. An avatar with a blush blendshape uses it. One without it gets the cheek quads, and they turn with her head. `BlushSettings.Enabled = false` clears both.
6. The Console shows one `Blush:` line when she spawns, and no errors if a shape is missing.
