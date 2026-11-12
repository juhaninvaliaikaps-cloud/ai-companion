# User mood

`UserMoodDetector` reads the user's own message and returns an emotion and an intensity (JUH-21). The avatar uses it to react before the model has answered, and the model gets it as one hint line.

Everything here runs on this machine. Nothing is sent anywhere by the detector.

## What it returns

`UserMoodDetector.Detect(text)` returns a `UserMood`:

| Field | Meaning |
| --- | --- |
| `Emotion` | A `UserEmotion`: `Neutral`, `Happy`, `Sad`, `Angry`, `Anxious`, `Surprised`, `Affectionate`, or `Confused`. |
| `Intensity` | 0 for neutral, otherwise up to 1. |

`UserEmotion` is the user's feeling. It is a different enum from `Emotion`, which is the avatar's face.

## How it decides

The detector uses word rules, not a model. All of it is in `AICompanion.Core`, with the word lists in `UserMoodLexicon`.

1. The first 2000 characters are split into lower-case words. Apostrophes are removed, so "don't" is `dont`.
2. Phrases are matched first, longest first: "i love you", "no way", "dont understand". A matched phrase uses up its words.
3. Each remaining word is looked up. English words match exactly. Finnish also has stems that match the start of a word, so `iloi` covers "iloinen" and "iloisia".
4. Each hit looks back in the same clause. A comma or a full stop ends the clause.
   - "very", "really", "so", "tosi" within two words multiply the weight by 1.5.
   - "a bit", "slightly", "vähän" within two words multiply it by 0.6.
   - "not", "dont", "never", "en", "ei" within three words negate it. A negated happy or affectionate hit counts as mildly sad. Any other negated hit is dropped, so "not sad" is neutral.
   - "no" negates only the word directly after it ("no fun"). "No I'm happy" is still happy.
5. Emoticons and emoji add to the score: `:)`, `:(`, `<3`, and common emoji.
6. `?!` adds surprise. `??` adds confusion.
7. The emotion with the highest total wins. A total under 0.5 is neutral. On a tie, distress wins: angry, sad, anxious, affectionate, happy, surprised, confused.
8. Intensity is `0.25 + 0.25 × total`. Each `!` adds 0.1, up to three. Two or more shouted words add 0.15. The result is clamped to 1.

To teach it a word, add the word to `UserMoodLexicon` and add a case to `UserMoodTests`.

## Where it is used

`ChatPanel.TrySend` calls the detector once per sent message, typed or spoken.

- **The face.** `UserMoodReaction.TryGetCue` turns the mood into an emotion-only `AnimationCue`, and the panel passes it to `AnimationDirector.Play`. She smiles back at joy, looks surprised at surprise, shy at affection, thoughtful at confusion, and concerned at sadness, anger, or worry. The intensity is kept between 0.3 and 0.7. A neutral mood leaves her face alone. The cue in the reply replaces this face when the reply ends. Pausing reactions in the tray also stops it, because it goes through the director.
- **The prompt.** `UserMoodPrompt.Append` adds one line after the system prompt and the director suffix, for example: `Mood hint: the user's latest message reads as sad (clear).` A neutral mood adds nothing. The line goes to the same provider as the message, so private mode is not affected.
- **The log.** One line per message, such as `User mood: sad 0.55`. The message text is not logged.

## Limits

- It does not understand sarcasm, context, or mixed feelings. It is a guess from wording, and the prompt line tells the model so.
- Only English and a small Finnish word list are covered. Finnish "no" as a filler ("no kiva") is read as a negation.
- There is no setting to turn it off on its own yet. The tray pause stops the face reaction.
- It runs when a message is sent. Reacting while the user is still typing or talking is JUH-23.

## Tests

`UserMoodTests` covers neutral input, each emotion in English and Finnish, emoticons, negation, the comma boundary, phrases, intensifiers, exclamation marks, shouted words, the 2000-character limit, the speed budget, the face mapping, and the prompt line. They run in EditMode with no scene.
