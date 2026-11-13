# Happi UI overhaul: design (JUH-104, JUH-108)

Approved by Jussi on 2026-11-12. This replaces the earlier "starter look". Product name in
the new UI is **Happi** (window title, setup wizard, settings header). **Leia** stays the
companion character's default name.

![Approved mockup](radial_mockup.png)

## The idea

Happi is a desktop companion, not an app window, so the main way to use her happens **on
her**, like The Sims: right-click her and a radial wheel pops up above her head with her
main functions. Right-click a specific body part and a smaller wheel opens for that part.
The wheel is only a new way to reach the same behaviour code (the presenters from PR B),
so no feature moves or gets lost. The settings panel stays for rarely used things.

## Palette (one theme object: `Assets/_Project/Scripts/UI/NewUI/HappiTheme.cs`)

| Role | Hex | Notes |
| --- | --- | --- |
| Wheel / panel background | `#3B6FB6` | medium blue, about 90% opaque; lighter so it reads on dark desktops |
| Borders / outlines | `#7FA8E0` | slice dividers and the ring outline |
| Hover | `#9FD4FF` | pale sky blue, **dark text** `#0B1530`, soft glow, slice pops out slightly |
| Text / icons | `#EAF2FF` | |
| Disabled / refused | `#B4C3DC` | e.g. a bratty persona refusing |
| Intimate / affectionate accent | `#FF7EB6` | kiss, hold hand and the lips wheel light up pink instead of blue |

The same palette is the base theme for the new chat, settings and wizard. Fonts, sizes,
corner radii, spacing and animation timings live in the same class. Sizes are in pixels at
100% Windows scaling and are multiplied by `HappiTheme.UiScale(Screen.dpi)` (1.0–2.5) so text
stays readable at 100–200%.

## Main wheel (above her head)

- Ring of **8 equal pie slices** around a round centre button. Thin gaps separate the
  slices; the ring has a thin light-blue outline. Centre: her name (default "Leia") with a
  small "right-click" hint.
- Fixed order, clockwise from the top (Settings sits top-left, just before Talk):
  1. **Talk** – open/focus the chat.
  2. **Watch my screen** – toggle the screen look and pick a monitor.
  3. **Mood** – her current mood and why (JUH-103 mood state).
  4. **Persona** – quick switch.
  5. **Do something** – submenu of existing animations/reactions only.
  6. **Leave me alone / Come back** – hide or mute her (focus mode).
  7. **Memories** – what she remembers, with the option to forget something.
  8. **Settings** – rarely used things.
- Labels short, centred, bold, wrapping to two lines.
- Hovered slice: pale sky blue, pops out, soft glow, dark text. Others: medium blue, near-white text.
- Slices can be greyed out (disabled colour) by mood or persona; a hook exists, logic stays simple.
- Follows her when she moves, stays on screen near monitor edges, closes on Escape, a click
  outside, or after an action.
- Right-click is caught **only over her**. Everywhere else stays click-through.
- Out of scope: keyboard shortcuts, last-used-first order.

## Body-part wheels

- Reusable component: any number of slices, a centre label, per-slice accent colour, and a
  thin connector line to the anchor point (mockup: pink line from her lips to the wheel).
- Hook point for JUH-101 hit zones. Planned sets (not built now): lips (kiss / touch / say
  something), head (pat / stroke hair), hands (hold hand / high five / pull along), cheeks
  (poke / pinch). No new touch reactions are added now.
- Adult options stay hidden behind the existing 18+ gate (`IntimateExpressions.Active`) and stay local.

## Chat (JUH-108 visual-novel playback)

- Each reply is split by a tolerant parser into narration (`*asterisks*`) and speech;
  unmarked text is speech.
- "Voice narration" setting, default off: narration is shown but not voiced.
- Current line highlighted. Auto-play and Step modes; in Step mode Next, a click or Space
  advances exactly one segment. Show all / Skip.
- The next speech line's TTS is prefetched while the user reads.
- Animation / reaction / mood cues fire at their own segment.
- One message object per message, last 100 messages, 6000 characters each (same as #64).

## Technology

uGUI + TextMeshPro, built in code like the old panels so the overlay's uGUI raycast
click-through keeps working. See `DECISIONS.md`.

## Rollout

The old UI stays the default. The new UI is behind **New UI (preview)** (Settings →
Overlay), off by default, or the `-newui` launch argument. With it off the app behaves
exactly as on `main`.
