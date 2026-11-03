# Expressions

The face is data. `Assets/_Project/Resources/ExpressionPresets.json` lists a weight for each channel of each emotion. `EmotionFace.EmotionPose` reads that file (with the same text as a fallback through `Resources`) and scales every weight by the cue intensity. A channel or a blendshape that the avatar does not have is skipped.

`Companion.controller`, the Gestures layer, and the clip names are unchanged. Emotions and talking are face-only: the runtime does not write the animator `Emotion` or `Talking` parameters, so the body stays in Idle, in Walk, or on the gesture layer. The 2D portrait and the 3D face show the expression.

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

`ExpressionPresetMap` matches VRM presets (`Joy`, `Fcl_ALL_Joy`, `Fcl_BRW_Sorrow`, `Fcl_MTH_Angry`, `Fcl_EYE_Joy`), ARKit (`mouthSmileLeft`, `mouthFrownLeft`, `browInnerUp`, `browDownLeft`, `eyeSquintLeft`, `eyeLookDownLeft`, `eyesLookDown`), Daz-style names (`eCTRLMouthSmile`, `eCTRLFrown`, `eCTRLBrowsUp`, `eCTRLEyeSquintL`, `eCTRLBlush`), a single-letter prefix such as `M_Joy`, and common MMD morphs (`にこり`, `笑い`, `困る`, `怒り`, `びっくり`, `まばたき`, `照れ`, `あ`). `Fcl_HA_Fung*` bangs and `tongueOut` are ignored. Blink still uses only blink and eye-close names, so a look-down shape does not blink.

When the avatar is bound, the Console logs one `Expressions: bound ...` line, one `Expressions: unclassified ...` sample, and one line per emotion:

```text
Expressions: sad -> 2/3 shapes found, missing eyeLookDown
Expressions: embarrassed -> 4/6 shapes found, missing blush, eyeLookDown
```

A cue that reaches the face logs `Expression: sad` (or `embarrassed`, `shy`, `flustered`, `concerned`). Portrait rows in the catalog store a neutral body emotion and put the real name in `portrait`. That name drives the 2D still and the 3D face, so `gesture="embarrassed"` blushes even though the catalog emotion is neutral. `embarrassed_worried` uses the embarrassed face. `angry` (the pouty still) uses the sad face.

While she is talking, lip sync replaces `aa`, `ih`, `ou`, `ee`, and `oh`. Smile, brows, squint, look-down, and blush stay. A procedural blink takes the max of the emotion lids and the blink, and does not write the mouth.

## Blush

`BlushSettings.Enabled` is the global switch (`true`). Set it to `false` and both the shape and the overlay stay off.

If the mesh has a blush shape (`blush`, `cheekBlush`, `eCTRLBlush`, `tere`, `照れ`, `赤面`), that shape gets the weight. Otherwise two soft quads parent to the head bone and use `AICompanion/CheekBlush`, a URP transparent shader. The texture is built in `BlushTexture`: a pink disk and faint diagonal hatch, original to this project. The quads sit at `BlushPlacement.LeftLocal` and `RightLocal` in head-local space (+Z toward the face). They stay hidden until the blush weight is above zero, which is embarrassed, shy, flustered, and a little of concerned. One Console line says which path ran, and `Blush: showing` is logged when the quads actually turn on:

```text
Blush: blendshape
Blush: procedural overlay
Blush: showing
Blush: no head bone, overlay skipped
Blush: off
```

## Prompt

`AnimationDirector.SystemPromptSuffix` appends the catalog, `AnimationCueParser.Instruction`, and one line of allowed emotion values. Portrait rows (`embarrassed`, `shy`, `flustered`, `concerned`, and the others) are catalog ids. Either of these reaches both the portrait and the 3D face:

```text
<animation emotion="embarrassed" intensity="0.9" />
<animation emotion="neutral" gesture="embarrassed" />
```

## Tests

`ExpressionPresetTests` covers preset lookup, unknown channels, VRM / ARKit / Daz / MMD names, portrait-name resolution, the found/missing coverage line, blush strength, lip-sync priority over the mouth, and the blink leaving blush alone. Rendering the overlay is a manual check.

## Local check

Build the Windows player and talk to her.

1. Keys **1**–**5** still select neutral, happy, sad, surprised, and thinking on the face. The body stays in Idle unless she is walking or playing a gesture.
2. A reply whose cue is `emotion="embarrassed"` or `gesture="embarrassed"` lowers her eyes and brings in a strong blush. `shy` is a lighter blush and a small smile. `flustered` blushes and opens the mouth. `concerned` or `worried` raises the inner brows without that strong blush. The same cue updates the 2D portrait when **Show expression portrait** is on.
3. `happy` near 1, or the laugh gesture, smiles with the eyes and opens the mouth. A lower intensity is the gentler smile.
4. While audio is playing, the mouth follows the words. Brows, smile, and blush stay on the emotion. She still blinks.
5. An avatar with a blush blendshape uses it. One without it gets the cheek quads, and they turn with her head. `BlushSettings.Enabled = false` clears both.
6. On spawn the Console shows `Expressions: bound ...`, one `Expressions: <emotion> -> N/M shapes found` line per emotion, and one `Blush:` line. Each cue adds `Expression: <emotion>`. `Blush: showing` appears when the cheek quads turn on.
