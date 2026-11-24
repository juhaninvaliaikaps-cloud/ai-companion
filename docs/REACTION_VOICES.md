# Reaction voices

Short sounds in her own voice: a giggle, "eh?", "hmph", "oh!", "mmm~", a sigh. They play when the expression she is already showing has a clip. Chat text is not read again, and nothing in this path is sent to a cloud model, cloud TTS, or a log of what was said.

## Where the files go

Do not put the wavs in the git repository. This repo is public. The player reads them from:

`%APPDATA%\ProjectLeia\reaction-voices\`

On a typical Windows account that is `C:\Users\<you>\AppData\Roaming\ProjectLeia\reaction-voices\`. Put `manifest.json` in that folder and the wav files next to it. If the roaming folder is not there, the player looks in `Application.persistentDataPath\ProjectLeia\reaction-voices`. If neither folder exists, or the manifest is missing or broken, she stays quiet and the Console logs one line. A missing wav is skipped. The others still play.

The folder is not encrypted, not copied by export, and not deleted by wipe. It is a voice pack, not a conversation.

## manifest.json

Each row is one variation. Several rows can share an expression. A new variation is another row; no code change. The root can be an array, one object, or an object with a `reactions` array. An expression is the `expression` field, or the file stem of `portrait_file` when that field is absent (`teary_happy.png` is `teary_happy`). A path, a `..`, or a wav name that is not a file in this folder is ignored.

```json
{
  "reactions": [
    {"reaction":"happy_giggle","label":"Happy giggle","portrait_file":"teary_happy.png","wav":"happy_giggle.wav","duration_s":0.34},
    {"reaction":"flustered_eh","label":"Flustered eh","portrait_file":"embarrassed_worried.png","wav":"flustered_eh.wav","duration_s":0.4},
    {"reaction":"pouty_hmph","label":"Pouty hmph","portrait_file":"tsundere.png","wav":"pouty_hmph.wav","duration_s":0.5},
    {"reaction":"surprised_oh","label":"Surprised oh","portrait_file":"shocked.png","wav":"surprised_oh.wav","duration_s":0.3},
    {"reaction":"teasing_mmm","label":"Teasing mmm","portrait_file":"smug_wink.png","wav":"teasing_mmm.wav","duration_s":0.6},
    {"reaction":"pleased_sigh_laugh","label":"Pleased sigh","portrait_file":"smug.png","wav":"pleased_sigh_laugh.wav","duration_s":0.7}
  ]
}
```

The thirteen expressions the pack is built around are `angry`, `bored`, `confused`, `crying`, `determined`, `embarrassed_worried`, `scared`, `shocked`, `sleepy`, `smug`, `smug_wink`, `teary_happy`, and `tsundere`. Any other name in the manifest is loaded the same way, so a later pack can add it.

Clips are 48 kHz mono wav. When an expression has more than one file, the next play picks at random and does not pick the file just used.

## What makes a sound

The existing systems already choose her expression. This only listens:

- A chat cue, including the quick face for the user's mood, offers the portrait the director just applied (or the emotion name when the cue did not name a portrait).
- A change of her mood rest look offers that rest expression. The same rest face does not play again.
- An interaction offers the expression it just set. That wins over the cue in the same moment, because the touch keeps its own portrait.
- **F9** offers the preview expression, so a pack can be heard without a reply. Neutral does not play.

One moment plays one clip. A different expression replaces a clip that is still going. The same expression does not start a second copy until that clip's `duration_s` has passed (half a second when the manifest omits it, and never longer than 8 seconds).

**F6** mute and tray pause keep her quiet, the same as speech and other reactions. A `finger_` expression plays only while Intimate expressions are on. The sound is local either way.

The Console line is `Reaction voice: happy_giggle → teary_happy`. It names the clip and the expression, not the chat.

## Try it on Windows

1. Create `%APPDATA%\ProjectLeia\reaction-voices\`.
2. Copy `manifest.json` and the wav files into that folder. The six test rows above match the current test pack.
3. Start Happi. The Console shows `Reaction voice: N clips from ...` or `Reaction voice: no pack at ...` when the folder is missing. A missing wav logs once and does not stop the others.
4. Press **F9** until the preview is `embarrassed_worried`. With `flustered_eh.wav` in the folder she says "eh?". `sleepy` on F9 stays quiet until that expression has a file.
5. Chat until a reply cue uses `teary_happy`, `tsundere`, `shocked`, `smug_wink`, or `smug`. She plays the matching clip. The spoken reply can overlap it; they use different audio sources.
6. Move her mood (see [MOOD.md](MOOD.md)) until the rest face changes to an expression the pack has, such as `embarrassed_worried` when she is anxious. She plays it once for that face, not on every tick.
7. Run an interaction whose expression is in the pack. That expression is the one that plays.
8. Turn on **F6** mute, or pause from the tray, and repeat. She stays quiet. Turn mute off and the next expression can play. The one that was skipped is not saved up.
9. Rename the folder away and start again. Chat, mood, and interactions still work. The Console has one missing-pack line.

`ReactionVoiceTests` covers the manifest, the missing file, the random variation, and when a cue, a mood change, or an event gets a clip. Hearing the wav is a check on the Windows player.

## Open questions

These are the small choices this version makes. They are easy to change.

- She plays on each new cue or interaction whose expression has a clip, and once when a mood rest expression changes. There is no extra cooldown. A screen-look remark replays the last cue, so it can play the sound again.
- The reaction uses its own audio source, so it can overlap her spoken reply.
- **F6** mute and tray pause silence reactions as well as speech. There is no separate switch.
- The first rest look after startup can play once, if that expression has a clip.
- Mood rest faces such as `pouty` and `smile` are not the thirteen pack names. The test hmph is `tsundere`, so an annoyed mood does not play it.
- The quick face for the user's own mood is `happy`, `concerned`, `surprised`, `shy`, or `thinking`. The test pack has none of those, so that face stays quiet until a cue names a packed expression.
- Wipe and export leave the folder.
- **F9** plays the preview name. `teasing` is not `smug_wink`, so that stop stays quiet with the test pack. `embarrassed_worried` plays.

