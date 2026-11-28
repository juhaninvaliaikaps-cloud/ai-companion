# Temperament sliders in chat: live results (JUH-236)

Jussi's goal (JUH-214): every personality slider should make a difference you can notice in chat, on the cloud model and on the local model. This page measures five sliders on both, before and after the JUH-236 change.

## How it was measured

The runner is `tools/temperament-live/run_live.py`, built on the paired design from JUH-222 (#209).

- **Synthetic only.** One made-up persona (Mira), a made-up two-message history and ten made-up everyday user messages, two per slider (`tools/temperament-live/fixtures.json`). The 18+ gate is closed. There's no real chat history, memory or secret, and the cloud model is used in public mode only.
- **The real prompt text.** `tools/temperament-live/PromptLines` is a small .NET tool that compiles `Assets/_Project/Scripts/Core`. It prints the three slider-dependent pieces from the production code:
  - `PersonaManner.Line`;
  - `PersonaManner.ConversationLine`;
  - `VoicePrompt.Block` for a neutral synthetic voice sheet.

  The runner puts them together in the order `ChatPanel.PersonaPrompt` uses for an ordinary chat turn, so the runner never copies these strings by hand.
- **One slider at a time.** Low is step 2 (stored 11) and high is step 9 (stored 89), with every other slider at step 5 (stored 44). Each condition gets the same two messages and three seeds (4123, 7319, 8817): 6 replies per cell.
- **Both voice paths.**
  - **No cached voice:** her manner line is in the prompt, and no voice block.
  - **Cached voice:** the voice block is in, and the manner line isn't. That's what the app does once a voice reading exists.
- **Negative control.** The neutral profile (every slider at step 5) is sampled twice, with the seeds above and with three others (1009, 2203, 3301). The gap between the two is the noise floor.
- **Models.**
  - **Local:** `happi-qwen35-9b` in Ollama 0.40.7 on 127.0.0.1, with the app's chat options: `num_ctx` 8192, temperature 0.7, and `RoleplayOptions` (repeat penalty 1.08 over 1024 tokens, `min_p` 0.05). `"think": false` is set, because without it this model returns no visible text at all; that's the separate production bug JUH-238 / #223.
  - **Cloud:** `deepseek-flash` at `api.deepseek.com`, thinking off as `CloudChatProvider` sets it, temperature 0.7.
- **Scoring.** A blind judge (`deepseek-flash`, temperature 0) answers one yes/no question per reply. It sees the conversation, the user's message and the reply, never the condition. I read a sample of replies next to the judge's answers, and they agreed.

  | Slider | The judge's question (yes = the trait shows) |
  | --- | --- |
  | Curiosity | asks a follow-up about a specific detail the user just mentioned (a generic question doesn't count) |
  | Assertiveness | proposes one concrete action the user could take now, as her own suggestion |
  | Directness | her first sentence plainly states her answer, choice or opinion |
  | Openness | shares something personal about herself (a feeling, memory, worry or preference) |
  | Playfulness | teases the user in a friendly way, or makes a joke or playful remark |

- **The verdict for a cell.**
  - **Noticeable:** high minus low is at least 50 points (3 of 6 replies) and bigger than the noise.
  - **Weak:** 33 to 49 points.
  - **Not noticeable:** less than that.
  - **Prompt identical:** the slider doesn't change the prompt at all on that path, so it can't change anything.

  Six replies per cell is a small sample. Between two runs with the same settings, a borderline cell can flip (cloud Directness with cached voice was "noticeable" in a first run and "not noticeable" in the one below). Read single cells as directional, and the pattern across both paths and both models as the result.

Reproduce:

```powershell
python tools/temperament-live/run_live.py --models cloud,local --label baseline
```

The cloud key is the app's own: `XAI_API_KEY`, or `%APPDATA%\ProjectLeia\secrets.json` unprotected with Windows DPAPI. It stays in memory only; it's never printed, logged or written. Every run's raw replies, prompts and summary go to `tools/temperament-live/results/<label>/`.

## Before: main at a1d81d8 (JUH-224's lines, no Openness or Playfulness line)

The share of replies where the trait showed, low → high. Raw data: `tools/temperament-live/results/baseline/`.

| Slider | Cloud, no cached voice | Cloud, cached voice | Local, no cached voice | Local, cached voice |
| --- | --- | --- | --- | --- |
| Curiosity | 100% → 100%: **not noticeable** | 83% → 100%: **not noticeable** | 83% → 100%: **not noticeable** | 100% → 100%: **not noticeable** |
| Assertiveness | 50% → 100%: noticeable | 50% → 83%: weak | 50% → 83%: weak | 33% → 100%: noticeable |
| Directness | 17% → 100%: noticeable | 83% → 100%: **not noticeable** | 0% → 83%: noticeable | 50% → 67%: **not noticeable** |
| Openness | 50% → 100%: noticeable | **prompt identical** | 100% → 100%: **not noticeable** | **prompt identical** |
| Playfulness | 0% → 100%: noticeable | **prompt identical** | 83% → 100%: **not noticeable** | **prompt identical** |

Noise (the neutral control's gap) was 0–17 points in most cells, with 33–50 in three. The full per-cell table, with the neutral rates and the noise, is in `results/baseline/summary.md`.

### What failed, and why

- **Openness and Playfulness do nothing once a voice reading exists.** Their only way into chat was the manner line ("open about her feelings", "teasing"), and `ChatPanel.PersonaPrompt` drops that line when a voice block is cached. `PersonaManner.ConversationLine`, the part that's added after the voice block, had no line for them.
- **The local model ignores the low end of Openness and Playfulness.** At step 2, without cached voice, she still shared about herself in 6 of 6 replies ("guarded about her feelings" in the manner line wasn't enough), and joked in 5 of 6 ("serious").
- **Curiosity doesn't change behaviour on either model.** At step 2 both models still asked a grounded follow-up in nearly every reply. The low line ("Let the user lead. Respond directly and usually leave it there.") is too soft against a user who has just shared news. JUH-222's calibration had already seen the same with the local model.
- **Directness separates without cached voice, but not reliably with it.** Low Directness with a voice block still often led with her take. This was borderline across runs.
- **Assertiveness separates on both models, weaker on some paths.**

## After 1: Openness and Playfulness lines in `ConversationLine`

`PersonaManner.ConversationLine` got an Openness and a Playfulness line at the same ends as Assertiveness and Directness: stored 30 or less (steps 1–3) and 70 or more (steps 8–10), with no line in between. It's appended after the voice block for ordinary chat only, so both sliders now reach the cached-voice path.

| Slider | Line |
| --- | --- |
| Openness, low | Keep your own feelings, memories and private life mostly to yourself. If asked about yourself, answer briefly without personal detail and turn the focus back to the user. |
| Openness, high | Share a little of yourself in most replies: a feeling, memory, worry or preference of your own, in a sentence that is about you, not only a reaction to the user. |
| Playfulness, low | Keep a plain, sincere tone. Do not tease the user, joke or use wordplay; respond to what they said straightforwardly. |
| Playfulness, high | Add a light touch: one friendly tease or small joke about what the user said, then stay kind and helpful. Leave it out when they are upset or serious. |

Measured again, with the same settings. Raw data: `tools/temperament-live/results/after-lines/`.

| Slider | Cloud, no cached voice | Cloud, cached voice | Local, no cached voice | Local, cached voice |
| --- | --- | --- | --- | --- |
| Openness | 33% → 100%: noticeable | 50% → 100%: **noticeable** (was prompt identical) | 67% → 100%: weak (was not noticeable) | 67% → 100%: weak (was prompt identical) |
| Playfulness | 0% → 100%: noticeable | 83% → 100%: **not noticeable** (was prompt identical) | 67% → 100%: weak (was not noticeable) | 67% → 100%: not noticeable (was prompt identical) |

- The high end works everywhere now.
- The low end is the gap that's left. With a cached voice, low Playfulness still joked in 5 of 6 cloud replies. On the local model, low Openness still shared in 4 of 6 and low Playfulness still joked in 4 of 6, on both voice paths.
