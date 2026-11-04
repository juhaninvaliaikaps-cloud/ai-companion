# Orientation on the desktop

## What you can do

- **Drag** left/right to turn her. The turntable is a full **360°**: every direction is reachable, including facing away from you.
- She **walks only sideways** along the taskbar. There is no depth axis, no walking away from the camera, and no distance scaling.

## What happens while walking

`JUH-85`: for the duration of a walk she turns **78° toward the travel direction** (`WalkFacing.TurnDegrees`), so a left or right walk reads as walking rather than sliding backwards. When the walk ends she returns to the yaw you had set (`AvatarAnimationController._extraYaw`).

## The single yaw limit

`AICompanion.Core.IdleYawPolicy` is the only place that decides how far the idle yaw may go:

```csharp
public const float MaxDegrees = 360f;   // full turntable
public static bool AllowsFullTurn => MaxDegrees >= 360f;
public static float Clamp(float yawDegrees) => AvatarFacing.WrapDegrees(yawDegrees);
```

`AvatarFacing.ApplyDrag` routes user drag through `IdleYawPolicy.Clamp`, so if an idle limit is ever wanted, changing that one constant is enough — no caller needs to change.

With 360° the call is a **wrap**, not a clamp: 181° becomes −179°, and a 720° drag returns to the starting yaw.

## What was deliberately rejected (JUH-88)

| Option | Why not |
|---|---|
| Clamp the idle yaw to ±60–90° | Jussi wants free 360° rotation |
| 2.5D depth (walk away = move up the screen, scale down, walk-away clip) | she must move only sideways |

The remaining idea, still not built, is a **side-step walk clip** used when her facing is more than ~90° from the travel direction. That needs a new procedural clip and a director rule; the ticket's policy-plus-tests slice is delivered here.

## Tests

`IdleYawPolicyTests`:

- the turn is a full turntable (`MaxDegrees == 360`, `AllowsFullTurn`)
- every direction stays reachable, including 180° and beyond ±180 wrapping
- a half turn via drag stays a half turn, and a 720° drag returns to the start

`WalkFacingTests` covers the 78° travel turn and its offset.
