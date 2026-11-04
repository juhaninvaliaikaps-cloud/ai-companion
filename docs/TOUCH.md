# Touch reactions

Click her body and she reacts: a face change, a small gesture and a short blush. Hold a hand and the blush stays while you hold.

## Zones

Zones are **humanoid bones**, so they follow whichever avatar is loaded:

| Zone | Bone | Reaction |
|---|---|---|
| `head` | Head | happy face, nod, light blush |
| `hand_left` / `hand_right` | Left/RightHand | warm face, stronger blush, **hold** |
| `shoulder_left` / `shoulder_right` | Left/RightShoulder | shrug, faint blush |

Data lives in `Assets/_Project/Resources/TouchZones.asset` (`TouchZoneSet`): id, bone, radius in screen pixels, emotion, intensity, gesture, blush, cooldown, `hold`. A new zone — or a future adult-mode pack — is an asset edit, not a code change.

## How it hooks in

- `OverlayWindow` already knows when the pointer is on her **silhouette** and not over UI, so it dispatches `IAvatarTouch.OnAvatarTouch` / `OnAvatarRelease` there. Click-through, window dragging, yaw dragging and the chat panel are unchanged.
- `AvatarTouchReactions` projects the bones to screen space, picks the nearest zone with `TouchZonePicker` (pure, tested), gates repeats with `TouchCooldown` (per zone, unscaled time) and then calls the director plus `AvatarAnimationController.AddTouchBlush`.
- The component is created lazily at startup from the overlay if it is missing, and it loads `Resources/TouchZones.asset`. A missing asset just leaves touch off.
- `Settings → Avatar → Touch reactions` turns the feature off without touching assets.

## Tests

`TouchZoneTests`: pick inside a zone, nearest wins, outside hits nothing, zero radius ignored, per-zone cooldown, and a zone overriding the default cooldown.

## Not in this slice

- **Cheeks** — no humanoid bone, so a cheek zone needs a local offset from the head transform.
- **Held hand pose** — holding keeps the reaction and blush, but there is no dedicated hand-holding clip yet; that needs an animation asset.
- **Mood/affection dependence** and **adult-mode zones** — the data format supports them, the runtime does not branch on state yet.
- **Voiced lines** — reactions are silent; a line would go through the normal chat/speech path.

## The model feels the touch too

A click is not only a local animation. `AvatarTouchReactions.Touched` fires with the zone id and
`ChatPanel` turns it into a short event line, for example:

```
*Someone takes your hand and holds it.*
```

That line goes through the normal chat request, so the model answers in character with its own
cue, face and speech. If a reply is already in flight the touch is ignored rather than queued,
which keeps the conversation readable.

## Portrait

Each zone can name a portrait expression (`expression` in the asset: `happy` for the head,
`shy` for the hands). The touch cue carries it into `AnimationCue.EmotionField`, the director
writes it as the expression hint, and the 2D portrait frame switches with the 3D face instead of
falling back to neutral.