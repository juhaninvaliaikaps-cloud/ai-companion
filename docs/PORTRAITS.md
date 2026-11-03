# Expression portraits

The 3D face cannot show every expression. An optional drawn portrait sits in a rounded frame beside her head and follows that point on screen. It is off until you turn it on.

Press **F9** to cycle the portrait expressions without a model reply and without sending chat: concerned, embarrassed, shy, flustered, embarrassed_worried, angry, teasing, excited, loving, sleepy, happy, laughing, then neutral. Each stop stays on that expression and cycles its blink and talk frames, so you can check `sad_talk` while sad without her speaking. The console logs `Expression preview: <name>`. **F9** does not open the chat panel and does not send a greeting.

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

Blink and talk frames. Happy stays on the smile set. Laughing uses the happy still plus `happy_blink_half`, `happy_blink_closed`, and `happy_talk`.

`neutral_open`, `neutral_blink_half`, `neutral_blink_closed`, `neutral_talk`

`smile_open`, `smile_blink_half`, `smile_blink_closed`, `smile_talk`

`concerned_blink_half`, `concerned_blink_closed`, `concerned_talk`

`embarrassed_blink_half`, `embarrassed_blink_closed`, `embarrassed_talk`

`shy_blink_half`, `shy_blink_closed`, `shy_talk`

`flustered_blink_half`, `flustered_blink_closed`, `flustered_talk`

`pouty_blink_half`, `pouty_blink_closed`, `pouty_talk`

`sad_blink_half`, `sad_blink_closed`, `sad_talk`

`surprised_blink_half`, `surprised_blink_closed`, `surprised_talk`

`thinking_blink_half`, `thinking_blink_closed`, `thinking_talk`

`excited_blink_half`, `excited_blink_closed`, `excited_talk`

`happy_blink_half`, `happy_blink_closed`, `happy_talk`

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
  "sad": {
    "open": "10_sad",
    "blinkHalf": "sad_blink_half",
    "blinkClosed": "sad_blink_closed",
    "talk": "sad_talk"
  },
  "transitions": {
    "neutral->happy": { "inBetween": "05_smile" }
  }
}
```

`open` is required. `blinkHalf`, `blinkClosed`, and `talk` are optional. `blink_half` and `blink_closed` are accepted names for the same fields. A string value such as `"concerned": "02_concerned"` replaces that emotion's whole frame set with one still. Anything you leave out keeps the default, including the blink and talk frames.

If a named frame file is missing, that expression uses its still. If the still is missing too, it uses neutral, so the frame is never empty while the neutral open image is there. Each missing file writes one warning to Player.log and does not repeat on the next blink:

```text
Expression portraits: missing 'sad_blink_half'. Showing the still.
Expression portraits: missing '11_surprised'. Showing neutral.
```

Defaults:

| Emotion | Frames |
| --- | --- |
| neutral | `neutral_open`, `neutral_blink_half`, `neutral_blink_closed`, `neutral_talk` |
| happy, and a `smile` key | `smile_open`, `smile_blink_half`, `smile_blink_closed`, `smile_talk` |
| laughing | `01_happy`, `happy_blink_half`, `happy_blink_closed`, `happy_talk` |
| concerned | `02_concerned`, `concerned_blink_half`, `concerned_blink_closed`, `concerned_talk` |
| embarrassed | `03_embarrassed`, `embarrassed_blink_half`, `embarrassed_blink_closed`, `embarrassed_talk` |
| shy | `06_shy`, `shy_blink_half`, `shy_blink_closed`, `shy_talk` |
| flustered | `07_flustered`, `flustered_blink_half`, `flustered_blink_closed`, `flustered_talk` |
| angry, and a `pouty` key | `09_pouty`, `pouty_blink_half`, `pouty_blink_closed`, `pouty_talk` |
| sad | `10_sad`, `sad_blink_half`, `sad_blink_closed`, `sad_talk` |
| surprised | `11_surprised`, `surprised_blink_half`, `surprised_blink_closed`, `surprised_talk` |
| thinking | `13_thinking`, `thinking_blink_half`, `thinking_blink_closed`, `thinking_talk` |
| excited | `15_excited`, `excited_blink_half`, `excited_blink_closed`, `excited_talk` |
| sleepy, tired | `12_sleepy` |
| teasing | `14_smug_wink` |
| loving | `16_wink_smile` |
| embarrassed_worried | `08_embarrassed_worried` |

`laughing` is a catalog id the model can put in the cue (`gesture="laughing"`). The portrait uses the laughing frames. The 3D face uses happy, and no body clip plays. Happy itself stays on the smile frames.

The other names are already in the map. An unknown name uses neutral.

`04_neutral` and `05_smile` are stills you can point at. `05_smile` is the short in-between when she goes from neutral to happy.

## Motion

- Blink, when both blink frames are on disk: open → half → closed → half → open. Each frame holds 40–60 ms. The gap is 2–6 s. About one blink in five plays twice. A missing blink file stays on the still.
- Talk uses the talk frame from the expression she is holding (`sad_talk` while sad, `concerned_talk` while concerned). It does not switch to `neutral_talk` or `smile_talk` just because audio starts. The open and talk frames follow lip-sync loudness (`IMouthDriver.MouthOverride`). If that level never moves, they take turns on a short random hold. A missing talk file stays on the still.
- The portrait holds for the whole utterance. A neutral cue that arrives before the audio, or a quiet gap between clips, keeps the current set. About 1.5 seconds after she is quiet and the cue is neutral, that portrait hold releases. A new non-neutral expression replaces it immediately. **F9** preview flaps blink and talk on the cycled expression without starting that utterance.
- Changing emotion crossfades for 150–250 ms. If `transitions` has `from->to` with `inBetween`, that image holds the middle of the fade. Blink and talk wait until the fade finishes.
- The 3D face uses the same expression for the whole reply and returns to neutral about 0.8 seconds after speech ends. The next cue replaces it. The face does not go neutral when the mouth starts moving. The animator `Talking` bool stays unset, including while an upper-body gesture plays.

The portrait uses the expression name from the animation director (`IExpressionHint`) when that name is set, and `IAvatarAnimator.Emotion` otherwise. During an utterance it keeps the expression already on screen when the live cue is neutral. It does not parse chat and it does not edit `Companion.controller`.
