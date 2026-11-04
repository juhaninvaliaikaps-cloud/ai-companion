# Touch reactions

Click her body and she reacts: a face change, a small gesture, a short blush and a line sent to the model so she can answer in words. Hands can be dragged with IK.

## Zones

Zones are humanoid bones plus an optional local offset, so they follow whichever avatar is loaded.

| Zone | Bone | Drag | Reaction |
|---|---|---|---|
| `head` | Head | | happy face, nod, light blush |
| `hair` | Head + up | | nod, faint blush |
| `cheek` | Head + face offset | | flustered portrait, strong blush |
| `hand_left` / `hand_right` | Left/RightHand | **yes** | shy portrait, hold, IK drag |
| `forearm_left` / `forearm_right` | LowerArm | | shy portrait, light blush |
| `shoulder_left` / `shoulder_right` | Shoulder | | shrug |
| `back` | Chest + back offset | | surprised |
| `side` | Hips + side offset | | surprised "poke" |

Data lives in `Assets/_Project/Resources/TouchZones.asset` (`TouchZoneSet`): id, bone, local offset, radius in screen pixels, emotion, intensity, gesture, portrait expression, blush, cooldown, `hold`, `drag`. Adding a zone, or shipping a separate content pack, is an asset edit.

## Hand drag

Pressing a hand begins an IK drag (`AvatarHandIk` on the Animator object, because Unity only calls `OnAnimatorIK` there). The hand follows the pointer on a plane at the depth where it was grabbed, the IK weight eases in and out (8 per second), and releasing lets the animation take over again. The click reaction still fires on press, so the drag also produces a shy portrait and blush.

## How it hooks in

- `OverlayWindow` already knows when the pointer is on her **silhouette** and not over UI, so it dispatches `IAvatarTouch.OnAvatarTouch` / `OnAvatarRelease`. Click-through, window dragging, yaw dragging, the chat panel and the debug panel are unchanged.
- `AvatarTouchReactions` projects bones (plus offsets) to screen space, picks the nearest zone with `TouchZonePicker` (pure, tested), gates repeats with `TouchCooldown` (per zone, unscaled time), applies the reaction and raises `Touched`.
- `ChatPanel` subscribes to `Touched` and sends a short event line such as `*Someone takes your hand and holds it.*` through the normal chat request, so the model answers in character with its own cue, face and speech. While a reply is in flight the touch is ignored rather than queued.
- `Settings → Avatar → Touch reactions` turns the whole feature off.
- The component is created lazily from the overlay if missing, and loads `Resources/TouchZones.asset`; a missing asset leaves touch off.

## Tests

`TouchZoneTests`: pick inside a zone, nearest wins, outside hits nothing, zero radius ignored, per-zone cooldown, zone override, and the chat line for every zone family (head, hands, shoulders, hair, cheek, forearm, back, side, unknown).

Full suite: **468 tests, 467 passed** (only the known Whisper Windows path failure).

## Not in this slice

- Mood/affection gating and a real hand-holding pose (holding keeps the reaction; the IK drag is separate).
- A voiced line triggered *only* by touch — the model answers through the normal reply path instead.
- Adult-only content packs: the asset format supports extra zones, but that content is not part of this work.
