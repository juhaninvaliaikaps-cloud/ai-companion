# Expression portraits

The 3D face cannot show every expression. An optional drawn portrait sits in a rounded frame beside her head and follows that point on screen. It is off until you turn it on.

Press **F11** to cycle the portrait expressions without a model reply: concerned, embarrassed, shy, flustered, embarrassed_worried, angry, teasing, excited, loving, sleepy, happy, then neutral. The console logs `Expression preview: <name>`.

**Settings → Avatar → Show expression portrait.** The switch is `showExpressionPortrait` in `settings.json`. The default is off. Empty pixels outside the frame still click through. The frame itself catches clicks so the desktop under the picture is not pressed by accident.

## Where the images go

Do not put the pictures in the git repository. This repo is public. The player reads them from the same per-user folder as settings:

`%APPDATA%\ProjectLeia\portraits\`

On a typical Windows account that is `C:\Users\<you>\AppData\Roaming\ProjectLeia\portraits\`.

If that `portraits` folder is missing, the player looks in `Application.persistentDataPath\ProjectLeia\portraits` (the same fallback settings use when the roaming profile path is empty). If neither folder exists, or the neutral open image is missing, the frame stays hidden and the Console logs one warning.

The sheets are different sizes (448×504 and 320×360, and anything else you add). Each image is scaled to fit inside the frame and keeps its aspect ratio.

## File names

Expressions, one still each:

`01_happy`, `02_concerned`, `03_embarrassed`, `04_neutral`, `05_smile`, `06_shy`, `07_flustered`, `08_embarrassed_worried`, `09_pouty`, `10_sad`, `11_surprised`, `12_sleepy`, `13_thinking`, `14_smug_wink`, `15_excited`, `16_wink_smile`

Blink and talk frames:

`neutral_open`, `neutral_blink_half`, `neutral_blink_closed`, `neutral_talk`

`smile_open`, `smile_blink_half`, `smile_blink_closed`, `smile_talk`

PNG is preferred. JPG and JPEG work too. Leave the extension off in the mapping file (`01_happy`, not `01_happy.png`). A `.txt` or a path with `..` or a slash is ignored.

## portraits.json

Optional file in that same folder. It remaps emotion names to a still, or to a frame set. Anything you leave out keeps the default below. A broken file is ignored and the defaults are used. `"transitions": {}` clears the built-in in-between.

```json
{
  "neutral": {
    "open": "neutral_open",
    "blinkHalf": "neutral_blink_half",
    "blinkClosed": "neutral_blink_closed",
    "talk": "neutral_talk"
  },
  "happy": {
    "open": "smile_open",
    "blinkHalf": "smile_blink_half",
    "blinkClosed": "smile_blink_closed",
    "talk": "smile_talk"
  },
  "sad": "10_sad",
  "transitions": {
    "neutral->happy": { "inBetween": "05_smile" }
  }
}
```

`open` is required. `blinkHalf`, `blinkClosed`, and `talk` are optional. `blink_half` and `blink_closed` are accepted names for the same fields.

Defaults:

| Emotion | Frames |
| --- | --- |
| neutral | `neutral_open`, blink, talk |
| happy, and a `smile` key | `smile_open`, blink, talk |
| sad | `10_sad` |
| surprised | `11_surprised` |
| thinking | `13_thinking` |
| angry | `09_pouty` |
| sleepy, tired | `12_sleepy` |
| teasing | `14_smug_wink` |
| excited | `15_excited` |
| loving | `16_wink_smile` |
| concerned, embarrassed, shy, flustered, embarrassed_worried | `02`–`08` stills |

The animator emotions today are neutral, happy, sad, surprised, and thinking. The other names are already in the map, so a later emotion with that name picks up the still without a code change. An unknown name uses neutral.

`01_happy`, `04_neutral`, and `05_smile` are stills you can point at. Happy uses the smile frame set out of the box, and `05_smile` is the short in-between when she goes from neutral to happy.

## Motion

- Blink, when the current expression has both blink frames: open → half → closed → half → open. Each frame holds 40–60 ms. The gap is 2–6 s. About one blink in five plays twice.
- Talk, when the expression has a talk frame and she is speaking: the open and talk frames follow lip-sync loudness (`IMouthDriver.MouthOverride`). If that level never moves, they take turns on a short random hold. Expressions with no talk frame stay on their still.
- Changing emotion crossfades for 150–250 ms. If `transitions` has `from->to` with `inBetween`, that image holds the middle of the fade. Blink and talk wait until the fade finishes.

The portrait reads the emotion already chosen by the animation director (`IAvatarAnimator.Emotion`). It does not parse chat and it does not edit `Companion.controller`.
