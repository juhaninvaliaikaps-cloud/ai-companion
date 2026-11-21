# Game companion

She watches the user's game and says a short line now and then, like a friend on the couch. This page covers what is decided, how it works, and what is still open.

## Decisions (Jussi, 2026-11-21)

- **It is a cloud feature.** On an average PC (8 GB), the local model and a game can't share the graphics card. Commentary during play uses the cloud AI API. There is no local model while a game is running. Local chat can come back between matches.
- **Seeing the screen is cloud too, and rare.** A local vision model next to a game needs a powerful machine.
- **It has its own opt-in.** Game data going to the cloud needs consent of its own.
- **It is off in private mode and while the 18+ gate is open.** Neither allows the cloud.
- **Only official, read-only feeds are used.** That means CS2 and Dota 2 Game State Integration and Riot's official API. The app never reads or writes game memory, injects code, or hooks the game's renderer. Her overlay is a separate window, not something inside the game.
- **Fair play.** She reacts to what already happened and what the player saw. She never gives hidden information, timers, cooldowns, enemy positions or advice that would be an unfair advantage.
- **World of Warcraft is skipped for now.** Blizzard limits real-time use of the combat log, and the log is buffered for minutes (JUH-48).

## The commentary engine (JUH-46)

The engine is plain C# in `Scripts/GameCompanion` (assembly `AICompanion.GameCompanion`), with no scene objects. It is tested by `CommentaryEngineTests`.

- **`GameEvent`** is what happened, the same for every game: a match won or lost, a round won or lost, a kill, a death, a multi-kill, a streak, first blood, an ace, an objective taken or lost, the bomb planted, defused or exploded, low health, or a level-up. Each event has a moment: minor, normal or big. Its detail is a word or two ("dragon"), never a player name or a chat line.
- **`BigMomentDetector`** turns a quick run of kills into one event, for feeds that only count kills. Two kills within 10 seconds is a multi-kill; three or more is a big moment. A death or a new round starts the count over.
- **`CommentaryPacer`** decides when she speaks:
  - The cooldown between comments depends on the frequency: rare is 90 s, normal is 40 s, chatty is 15 s.
  - A big moment (a match won, an ace, a bomb defused, a triple kill, a 5-kill streak) can break the cooldown. It still never comes sooner than 8 seconds after the last comment.
  - Each frequency has an hourly budget (rare 8, normal 20, chatty 40), and big moments count against it. This keeps cloud calls rare.
  - Minor moments (a single kill, an assist, a level-up, low health) are never spoken about on their own. They are kept as context for the next comment, for up to 90 seconds and 6 events.
  - Quiet mode and push-to-mute (`MuteFor`) stop everything.
- **`CommentaryGate`** allows a comment only when the feature is on, the cloud opt-in is given, and neither private mode nor the 18+ gate is on.
- **`CommentaryPrompt`** writes the fair-play rules and the turn ("The user is playing Counter-Strike 2. Just now: their team won the round. Before that: they got a kill.").

## The game readers (JUH-48)

Each reader turns one game's own official feed into `GameEvent`s. They are in `Scripts/GameCompanion` and tested by `GameReaderTests`.

- **`Cs2GsiReader`** reads the payload CS2 posts. It covers the map phase (a match starts, a match is won or lost), the round result, the bomb (planted, defused or exploded) and the user's own kills, deaths and assists. It also reports an ace (5 kills in a round) and low health (under 25). A payload about a player the user is spectating is ignored.
- **`Dota2GsiReader`** reads the game state (a match starts, a match is won or lost) and the user's own kills, deaths and assists. It also reports kill streaks (3, 5, 7, 10), the ultimate levels (6, 12, 18, 25) and low health (under 20 %).
- **`LeagueLiveReader`** reads Riot's Live Client Data API: `https://127.0.0.1:2999/liveclientdata/eventdata`, plus `activeplayername` and `playerlist` for the user and the teams.
  - Kills and deaths are kept only when they involve the user: a kill, death, assist, first blood, multi-kill or ace.
  - Objectives (dragon, herald, baron, tower, inhibitor) are taken or lost from the user's team's point of view. Every player sees these announced.
  - Names in `eventdata` have no `#tag`, so names are compared without it.
  - Riot's third-party app approval is needed before release.
- **`GsiConfig`** writes the config file for CS2 and Dota 2, named `gamestate_integration_happi.cfg`. It points at `http://127.0.0.1:39271/` and carries a token for this install. It asks only for the user's own data, never `allplayers`.
- **`GsiListener`** receives those posts on 127.0.0.1 only. It drops any post without the token, and caps the body size and the queue. The bodies wait in a queue for the main thread, and nothing in it touches Unity.

**Installing the config (manual for now).** Copy the generated file to:
- CS2: `…\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\`
- Dota 2: `…\steamapps\common\dota 2 beta\game\dota\cfg\gamestate_integration\` (create the folder if it doesn't exist)

Then restart the game.

## Still to build

- The League poller: a request every second or so to the local API, accepting only Riot's certificate on 127.0.0.1:2999.
- Starting and stopping the GSI listener with the app.
- The glue that sends a comment through the cloud AI API with her persona, shows it in chat and speaks it.
- The settings: the feature switch, the opt-in, the frequency, quiet mode and the push-to-mute key.

## Open decisions for Jussi

- **Reactions without a model.** Should she react to game events with an animation and a short pre-written line through the local voice? This would need no model at all, and would also work in private mode.
- **Installing the game's config file.** Should the app write the GSI config file into the user's game folder itself, or should the user copy it in? Until this is decided, copying it is a manual, documented step.
- **WoW after-fight comments.** These could be based on encounter start and end, which don't need real-time data.
