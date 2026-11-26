# Ribbon Tabs visual prototype

JUH-199, isolated visual preview for the Ribbon Tabs and approved Happi chatbox
from JUH-198.

This is a **render-only prototype**. It does not touch the live overlay, chat,
settings, presenters, privacy rules, providers, persistence, room behavior or
production startup. Every destination and the Send control are inert and only
show a status line.

## Open it

No build or package install is needed:

```text
docs/ribbon-prototype/index.html
```

Open the file directly in Edge or Chrome, or serve the folder:

```powershell
python -m http.server 8099 --directory docs/ribbon-prototype
# http://127.0.0.1:8099/
```

The stage is a fixed **1672 × 941** composition matching the reference art.
The controls bar underneath changes the preview state. `?capture=1` hides the
controls for screenshots.

## Preview states

| State | URL | Shows |
| --- | --- | --- |
| Menu rest | `?state=menu` | six-row ribbon, no flyout |
| Menu hover | `?state=hover` | cyan hover on Profile |
| Profile | `?state=profile` | yellow illustrated Profile flyout |
| AI | `?state=ai` | yellow illustrated AI flyout |
| Voice muted | `?state=muted` | Unmute label and muted chat header |
| Action accent | `?state=accent` | one-shot ink accent |

The menu order is fixed:

```text
Mute / Unmute → Look now → Profile → AI → Settings → Close
```

Hover Profile or AI to open the matching flyout; move into the flyout to keep it
open. Escape or Close dismisses the flyout. The major ink accent is played by
Look now, Settings, Send and every flyout destination.

## Files

```text
index.html                    composition and sample data
styles.css                    visual treatment, matte buttons, clipped chat box
app.js                        preview state machine and inert callbacks
assets/                       the five JUH-198 reference PNGs
screenshots/                  captured states for the PR
```

The same five reference PNGs are under
`Assets/_Project/Resources/RibbonPrototype/` so an eventual Unity pass can load
them from Resources.

## Visual decisions

- Top-level rows are opaque black, gently staggered, with a mint leading edge.
- Hover is cyan with dark text; the label and hit area do not resize.
- The flyout is a yellow illustrated board; artwork sits in its own layer with
  `pointer-events: none`.
- Profile uses the Mood painting; AI uses the Screen painting.
- The chat header uses the Talk painting crop.
- The chat panel has clipped corners, a charcoal transcript, a cyan Happi accent,
  and the angled cyan Send control.
- No crossfade is used for the flyout; it opens at a stable size.
- Monitor-edge flipping and click-through are integration work; this fixed stage
  does not claim to prove them.

## Integration contract

The prototype exposes the visual pieces only:

```text
Ribbon menu         labels, order, hover and selected styles
Profile flyout      destination labels, artwork slot, inert callbacks
AI flyout           destination labels, artwork slot, inert callbacks
Chat header         Happi name, Private/Public badge, voice mute, window actions
Chat transcript     user/Happi rows and accent treatment
Composer            stable field and Send hit area
Ink accent          one-shot mark on action selection
```

Replace the inert callbacks with the existing presenters during integration. Do
not copy the preview's state or sample messages into production.

## Verification

The captures in `screenshots/` were rendered by headless Edge at 1672 × 941 with
the controls hidden. The Unity editor was not available on this machine (the
project/build drive was BitLocker-locked), so this work was verified as a
standalone browser prototype only. Unity import, `EditMode` tests and a Windows
player walkthrough remain unverified.
