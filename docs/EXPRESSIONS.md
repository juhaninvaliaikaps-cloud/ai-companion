# Expressions

The face is data. `Assets/_Project/Resources/ExpressionPresets.json` lists a weight for each channel of each emotion. `EmotionFace.EmotionPose` reads that file (with the same text as a fallback through `Resources`) and scales every weight by the cue intensity. A channel or a blendshape that the avatar does not have is skipped.

`Companion.controller`, the Gestures layer, and the clip names are unchanged. Emotions and talking are face-only: the runtime does not write the animator `Emotion` or `Talking` parameters, so the body stays in Idle, in Walk, or on the gesture layer. The 2D portrait and the 3D face show the expression.

## Sheet

The drawn sheet is the target. It is not in the repo.

| Drawing | Emotion | What the weights do |
| --- | --- | --- |
| Laughing, open mouth | `happy` at a high intensity, and the `laugh` gesture (happy at 0.9) | Smile (`joy`), smile-eyes (`eyeSquint`), cheek squint (`fun`), jaw open (`aa`) |
| Gentle smile | `happy` at a lower intensity, or emotion `smile` | Same channels, scaled down, so the mouth barely opens |
| Sad | `sad` | Inner brows up, a frown (`sorrow`), gaze down, a slight squint. No lowered outer brow |
| Surprised | `surprised` | Wide eyes, inner brow and both outer brows up, jaw slightly open. A whole-face `surprised` shape gets the same weight when the mesh has one |
| Thinking | `thinking` | One outer brow up (`browOuterUpLeft`), a look up and to one side, a small pucker and a press of the lips |
| Concerned | `concerned` (`worried` is the same) | Inner brows up harder than sad, a milder frown, eyes a little down. No blush |
| Strong blush, eyes down | `embarrassed` | Blush at 1, gaze and lids lowered, a little inner brow |
| Neutral | `neutral` | Everything at 0 |
| Light blush, small smile | `shy` | Blush about 0.38, lowered eyes, a small smile |
| Blush and an open mouth | `flustered` | Blush near 1 and an open jaw |
| Embarrassed and worried | `embarrassed` | Strong blush plus the inner brow. `concerned` is the same worry with no blush |

## 2D portrait expressions

The default map in `PortraitMap.CreateDefaults()` includes the drawn portrait set. A file is looked up by stem in the portraits folder; a missing blink or talk frame falls back to that expression's still, and a missing still falls back to neutral.

| Expression | Frames | 3D face |
| --- | --- | --- |
| `crying` | open + blink + talk | sad |
| `teary_happy` | open + blink + talk | happy |
| `shocked` | open + blink + talk | surprised |
| `confused` | open + blink + talk | thinking |
| `bored` | open + blink + talk | neutral |
| `scared` | open + blink + talk | concerned |
| `tsundere` | open + blink + talk | shy |
| `determined` | open + blink + talk | neutral |
| `angry` | open + blink + talk | sad |
| `smug` | open + blink + talk | happy |
| `smug_wink` | open + talk only (a wink) | happy |
| `sleepy` | open + blink + talk | neutral |
| `embarrassed_worried` | open + blink + talk | concerned |
| `flustered_heavy` | open + blink + talk | flustered |
| `seductive` | open + blink + talk | shy |
| `kiss` | open + blink + talk | happy |
| `finger_suck` | open + blink + talk | flustered |
| `finger_suck_deep` | open + blink + talk | flustered |
| `finger_suck_hold` | still (first frame of the hold sequence) | flustered |
| `finger_pull` | still | shy |
| `finger_pull_start` | still | shy |

The `finger_*` set is suggestive rather than explicit. It is gated by **Settings -> Avatar -> Intimate expressions (18+)**, which is off by default and works only in Private mode (local model). While it is off, those portraits are not registered or resolved (the cue falls back to the normal flustered or shy face), and their names are not in the prompt. When it is on, the files load only from `%APPDATA%\ProjectLeia\portraits-adult`; if that folder is missing, the feature stays off and one warning is logged. The finger referred to is the user's finger. `finger_suck_hold_00`..`07` is a drawn sequence, but the panel shows a single frame, so only `_00` is registered.

### Boundaries (JUH-112)

Every system prompt carries `IntimateBoundaries` between the persona text and the cue catalog, so no persona can remove it:

- **Always:** intimacy is mutual and willing. She is never shown as unwilling, pressured, coerced, scared or distressed in an intimate moment. If the user pushes a scene that way, she says no and steers somewhere else, even in role-play.
- **Gate closed** (setting off, not Private mode, or no `portraits-adult` folder): no sexual or explicit scenes; affection stays light.
- **Gate open:** `IntimateExpressions.KeepsLocal` makes every outbound check treat the session as private, so chat, TTS, vision and the voice agent stay local even if a caller's private flag is stale.

A prompt can steer an uncensored local model but cannot force it. The hard limits are the cue gate (`finger_*` only resolves when the gate is open) and the local-only routing.

The `Voice: TTS request starting` log line names the engine that really runs (`Kokoro`, `Piper` or `xAI`). Before JUH-112 it printed `engine=xAI` for Kokoro too.

Synonyms choose the same portraits: `afraid`/`fear` -> `scared`, `annoyed` -> `tsundere`, `teasing` -> `smug`, `focused` -> `determined`, `tired` -> `sleepy`, `love` -> `kiss`, `flirty` -> `seductive`, `overwhelmed` -> `flustered_heavy`. A strong `sad` (intensity 0.9 or more) uses `crying`; a strong `surprised` uses `shocked`. Unknown words still fall back to neutral. `pouty` keeps its older drawing.

The prompt lists these names and the synonym rules in `EmotionNames.CatalogLine`, so the model can choose them. The 3D face uses the existing presets in the last column; the drawn portrait and the face may differ in how expressive they are.
## Names

`ExpressionPresetMap` matches VRM presets (`Joy`, `Fcl_ALL_Joy`, `Fcl_BRW_Sorrow`, `Fcl_MTH_Angry`, `Fcl_EYE_Joy`), ARKit (`mouthSmileLeft`, `mouthFrownLeft`, `browInnerUp`, `browOuterUpLeft`, `browOuterUpRight`, `browDownLeft`, `eyeSquintLeft`, `cheekSquintLeft`, `eyeLookDownLeft`, `eyeLookUpLeft`, `eyeLookOutLeft`, `eyeLookInRight`, `mouthPressLeft`, `mouthPucker`), Daz-style names (`eCTRLMouthSmile`, `eCTRLFrown`, `eCTRLBrowsUp`, `eCTRLEyeSquintL`, `eCTRLCheekSquintL`, `eCTRLBlush`), a single-letter prefix such as `M_Joy`, and common MMD morphs (`にこり`, `笑い`, `困る`, `怒り`, `びっくり`, `まばたき`, `照れ`, `あ`). `cheekSquint` is `fun`, so a smile still lifts the cheeks when there is no `fun` morph. `browOuterUpLeft` is its own channel so thinking can raise one brow. `eyeLookOutLeft` and `eyeLookInRight` glance to one side; the opposite pair is left alone so the eyes do not cancel. `Fcl_HA_Fung*` bangs and `tongueOut` are ignored. Blink still uses only blink and eye-close names, so a look-down shape does not blink.

When the avatar is bound, the Console logs one `Expressions: bound ...` line, one `Expressions: unclassified ...` sample, and one line per emotion:

```text
Expressions: sad -> 5/5 shapes found
Expressions: surprised -> 7/7 shapes found
Expressions: embarrassed -> 6/6 shapes found
```

A channel counts as found when a blendshape is bound to it. `surprised` also counts when wide eyes, the inner brow, and an outer brow are bound, because those shapes stand in for a missing whole-face morph. `fun` counts when a squint is bound. `blush` counts because the cheek overlay is the stand-in. The `Blush:` line still says whether a shape or the overlay ran. On a mesh with no `surprised` shape, that weight is copied onto the wide eyes, the brows, and a slightly open jaw before it is shown. A missing `fun` shape is added onto the squint. `cheekSquint` is bound as `fun`, so Asuna keeps the cheek weight on its own shape.

Each reply logs the emotion attribute before it is interpreted, then the face that was applied:

```text
Cue emotion: sad
Expression: sad
```

`Cue emotion: (none)` means the tag had no emotion attribute. An unknown word is still logged, and the cue stays neutral. Portrait rows in the catalog store a neutral body emotion and put the real name in `portrait`. The prompt lists that name as `face:`. That name drives the 2D still and the 3D face, so `gesture="embarrassed"` blushes even though the catalog emotion is neutral. `embarrassed_worried` uses the concerned face and its own drawn frames. `angry` uses the sad face and its own drawn frames; `pouty` keeps the older pouty still. `sleepy` keeps a neutral face and its drawn frames.

While she is talking, lip sync replaces `aa`, `ih`, `ou`, `ee`, and `oh`. Smile, brows, squint, look-down, look-up, a side glance, a lip press, and blush stay. A thinking pucker is a mouth shape, so speech replaces it until the line ends. A procedural blink takes the max of the emotion lids and the blink, and does not write the mouth.

The 3D expression stays up for the whole reply. Starting to talk does not put the blendshapes back on a neutral talking face, and an upper-body gesture does not write the animator `Talking` bool. The mouth layers on top: lip sync and the procedural jaw write only those five mouth channels. The portrait keeps that expression and uses its own talk frame (`sad_talk` while sad, and the same for the other sets that have one). The face returns to neutral only after the reply's speech has finished (synthesis and the clip queue both count, so a pause between sentences keeps the expression) and then been quiet for about 0.8 seconds, or sooner when the next cue arrives. The portrait's own hold releases about 1.5 seconds after she is quiet and the cue is neutral. A `neutral` cue clears the face immediately. The pause between the cue and the first syllable does not count as the end of speech, so a slow TTS start cannot drop the face. When the face hold lets go, the Console logs `Expression: neutral`.

## Blush

`BlushSettings.Enabled` is the global switch (`true`). Set it to `false` and both the shape and the overlay stay off.

If the mesh has a blush shape (`blush`, `cheekBlush`, `eCTRLBlush`, `tere`, `照れ`, `赤面`), that shape gets the weight. Otherwise two soft quads parent to the head bone and use `AICompanion/CheekBlush`, a URP transparent shader. The texture is built in `BlushTexture`: a pink disk and faint diagonal hatch, original to this project. The quads sit at `BlushPlacement.LeftLocal` and `RightLocal` in head-local space (+Z toward the face). They stay hidden until the blush weight is above zero. That weight is embarrassed, shy, and flustered. Concerned, sad, surprised, thinking, and happy stay at zero, so a concerned reply does not log `Blush: showing`. One Console line says which path ran, and `Blush: showing` is logged when the quads actually turn on:

```text
Blush: blendshape
Blush: procedural overlay
Blush: showing
Blush: no head bone, overlay skipped
Blush: off
```

## Prompt

`AnimationDirector.SystemPromptSuffix` appends the catalog, `AnimationCueParser.Instruction`, and `EmotionNames.CatalogLine`. The catalog line says when to use each emotion and says not to default to happy or embarrassed. Each portrait row also lists `face:` so the still name is visible even when the body emotion is neutral. Portrait rows (`embarrassed`, `shy`, `flustered`, `concerned`, and the others) are catalog ids. Either of these reaches both the portrait and the 3D face:

```text
<animation emotion="embarrassed" intensity="0.9" />
<animation emotion="neutral" gesture="embarrassed" />
```

## Tests

`ExpressionPresetTests` covers preset lookup, unknown channels, VRM / ARKit / Daz / MMD names, portrait-name resolution, the found/missing coverage line, blush strength, lip-sync priority over the mouth, and the blink leaving blush alone. `ExpressionHoldTests` covers the reply hold: it stays up before the first syllable, through speech, and for a short tail, and a new cue replaces it. Rendering the overlay is a manual check.

## Local check

Build the Windows player and talk to her.

1. Keys **1**–**5** still select neutral, happy, sad, surprised, and thinking on the face. The body stays in Idle unless she is walking or playing a gesture.
2. A reply whose cue is `emotion="embarrassed"` or `gesture="embarrassed"` lowers her eyes and brings in a strong blush. `shy` is a lighter blush and a small smile. `flustered` blushes and opens the mouth. `concerned` or `worried` raises the inner brows and does not blush. The same cue updates the 2D portrait when **Show expression portrait** is on. Sad, surprised, and thinking should be visually distinct from that worry: a frown and a downward look, wide eyes with both brows up, and one brow with a side glance.
3. `happy` near 1, or the laugh gesture, smiles with the eyes and opens the mouth. A lower intensity is the gentler smile.
4. While audio is playing, the mouth follows the words and the expression she was given stays on the face and the portrait until the audio has been quiet for a short moment. Brows, smile, and blush stay on the emotion. She still blinks. The next cue can replace that expression immediately. `Expression: neutral` is the line after the hold lets go.
5. An avatar with a blush blendshape uses it. One without it gets the cheek quads, and they turn with her head. `BlushSettings.Enabled = false` clears both.
6. On spawn the Console shows `Expressions: bound ...`, one `Expressions: <emotion> -> N/M shapes found` line per emotion, and one `Blush:` line. An ARKit face such as Asuna should report full coverage for every shipped emotion. Each reply logs `Cue emotion: <field>` and then `Expression: <emotion>`. `Blush: showing` appears when the cheek quads turn on, which is embarrassed, shy, and flustered.
