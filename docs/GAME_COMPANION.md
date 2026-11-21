# Game companion

She watches the user's game and says a short line now and then, like a friend on the couch. This page covers what is decided, how it works, and what is still open.

## Decisions (Jussi, 2026-11-21)

- **It is a cloud feature.** On an average PC (8 GB), the local model and a game can't share the graphics card. Commentary during play uses the cloud AI API. There is no local model while a game is running. Local chat can come back between matches.
- **Seeing the screen is cloud too, and rare.** A local vision model next to a game needs a powerful machine.
- **It has its own opt-in.** Game data going to the cloud needs consent of its own.
- **Private mode and an open 18+ gate allow pre-written lines only (Jussi, 2026-11-21).** Neither allows the cloud, so no model lines and no game data is sent. Pre-written lines run entirely on this PC with the local voice, so they keep working.
- **Only official, read-only feeds are used.** That means CS2 and Dota 2 Game State Integration and Riot's official API. The app never reads or writes game memory, injects code, or hooks the game's renderer. Her overlay is a separate window, not something inside the game.
- **Fair play.** She reacts to what already happened and what the player saw. She never gives hidden information, timers, cooldowns, enemy positions or advice that would be an unfair advantage.
- **World of Warcraft is its own feature (JUH-145).** Blizzard limits real-time use of the combat log, and the log is buffered for minutes (JUH-48). Comments after fights are not enough: she should follow the zone, the questing and what you're doing.
- **Her reactions come from her personality, through Pisara (JUH-49).** The temperament sliders and the relationship role decide how she sounds. A bratty persona laughs when you die and says "You suck!". The model goes first; pre-written lines in the same personality are the fallback.
- **The app installs the game's config file itself (JUH-146),** when the user presses Set up.
- **The settings get their own Games section (JUH-47).**

## The commentary engine (JUH-46)

The engine is plain C# in `Scripts/GameCompanion` (assembly `AICompanion.GameCompanion`), with no scene objects. It is tested by `CommentaryEngineTests`.

- **`GameEvent`** is what happened, the same for every game: a match won or lost, a round won or lost, a kill, a death, a multi-kill, a streak, first blood, an ace, an objective taken or lost, the bomb planted, defused or exploded, low health, or a level-up. Each event has a moment: minor, normal or big. Its detail is a word or two ("dragon"), never a player name or a chat line.
- **`BigMomentDetector`** turns a quick run of kills into one event, for feeds that only count kills. Two kills within 10 seconds is a multi-kill; three or more is a big moment. A death or a new round starts the count over.
- **`CommentaryPacer`** decides when she speaks:
  - The cooldown between comments depends on the frequency: rare is 90 s, normal is 40 s, chatty is 15 s.
  - A big moment (a match won, an ace, a bomb defused, a triple kill, a 5-kill streak) can break the cooldown. It still never comes sooner than 8 seconds after the last comment.
  - Each frequency has an hourly budget of model comments (rare 8, normal 20, chatty 40), and big moments count against it. This keeps cloud calls rare. Once it is used up she still reacts, with pre-written lines only (`CommentDecision.UseModel` is false). They cost nothing and don't count against the budget, but they do start the cooldown.
  - Minor moments (a single kill, an assist, a level-up, low health) are never spoken about on their own. They are kept as context for the next comment, for up to 90 seconds and 6 events.
  - Quiet mode and push-to-mute (`MuteFor`) stop everything.
- **`CommentaryGate.Decide`** gives the mode:
  - **Off:** the feature is off.
  - **Pre-written only:** private mode or the 18+ gate is on, there's no cloud opt-in, or no cloud AI API is set up. Nothing about the game is sent.
  - **Model first:** everything is allowed.
- **`CommentaryRoute`** picks the source of one line. It uses the model when the mode and the pacer allow it, and a pre-written line otherwise. A model reply that failed, came back empty or took longer than 6 seconds is replaced by a pre-written line, because the moment has passed.
- **`CommentaryPrompt`** writes the model's instructions and the turn:
  - `SystemText(personaPrompt, voice)` is her persona prompt, then her game style, then the fair-play rules.
  - The turn says what happened: "The user is playing Counter-Strike 2. Just now: their team won the round. Before that: they got a kill."

## Her personality in games (JUH-49)

`GameVoices.For(axes, relationshipRole)` turns her Pisara temperament sliders into a game voice. The first rule that matches wins:

| Voice | When | Sounds like |
|---|---|---|
| Tsundere | warmth ≥ 65, directness ≤ 30, volatility ≥ 50 | "Idiot! Be more careful!" |
| Shy | confidence ≤ 35, assertiveness ≤ 40 | "Ah! Oh no..." |
| Teasing (bratty) | playfulness ≥ 55, assertiveness ≥ 55 or directness ≥ 60, warmth ≤ 65; or a rival with playfulness ≥ 45 | "You suck! Ha!" and a laugh |
| Grumpy | warmth ≤ 40, volatility ≥ 60 or playfulness ≤ 35 | "And you're dead. Great." |
| Hype | energy ≥ 65, warmth ≥ 55 | "Noo! Get back in there!" |
| Sweet | warmth ≥ 60 | "Oh no, are you okay?" |
| Cool | everything else, including the middle of every slider | "Ouch." |

With the presets: Calm is Cool, Cheerful is Hype, Shy is Shy, Tsundere is Tsundere, Moody is Grumpy.

- **Model lines.** `GameVoices.StyleLine(voice)` goes into the model's instructions, so a model comment sounds like the same girl.
- **Pre-written lines.** `GameReactionCatalog` has at least three lines for each voice and moment: match start, win, loss, death, kill, big play, objective taken or lost, a tense moment, level-up. Each line comes with a face and a gesture (Laugh, Nod, Shrug or Wave) that the animator has today. New game animations can be added as Jussi makes them (JUH-50).
- **`GameReactionLines.Pick`** chooses a line and never says the same one twice in a row for a moment. A bomb defused or exploded gets no pre-written line, because the feed doesn't say whose bomb it was.

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

**Installing the config (JUH-146, automatic).** When the user presses **Set up** for CS2 or Dota 2, `GsiInstaller.Install` does the following:
1. Finds Steam. It tries the registry value `HKCU\Software\Valve\Steam\SteamPath` first (`WindowsSteamPath`), then `C:\Program Files (x86)\Steam` and `C:\Program Files\Steam`.
2. Reads `steamapps\libraryfolders.vdf` to list every library. Both the current and the old format work (`VdfReader`, `SteamLibraries`).
3. Finds the game's `appmanifest_730.acf` (CS2) or `appmanifest_570.acf` (Dota 2) and takes its `installdir`. A name that could leave the library (`..`, a slash, a drive) is refused.
4. Writes `gamestate_integration_happi.cfg` to:
   - CS2: `…\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\`
   - Dota 2: `…\steamapps\common\dota 2 beta\game\dota\cfg\gamestate_integration\`. Only this last folder is created, and only when the game's own `cfg` folder is there.
5. Reports the result: written, already set up, game not found, or couldn't write (with the reason).

Rules:
- It writes only on a button press, never in the background.
- **Remove** deletes the file only if its first line is ours (`"Happi game companion"`), so a file Happi didn't write is left alone.
- Dota 2 also needs `-gamestateintegration` in its Steam launch options. Happi tells the user how to add it and doesn't edit Steam's own config, because that is fragile.
- The game reads the file at start, so restart the game after setup.
- League needs no file.

## At runtime (JUH-47)

`GameCompanionController` (Settings assembly, on the app object) connects everything.
- **The feeds:**
  - The GSI listener runs while the game companion is on, CS2 or Dota 2 is ticked and a token exists. It restarts when the port or the token changes.
  - Posts are routed by the game's app id (`GsiRouting`).
  - The League poller asks the local API once a second during a match, and every 5 seconds while no match is on. Its certificate handler is set only on those loopback requests. Pinning Riot's root certificate is an open item.
- **Each event** goes through `GameCommentarySession`: big-moment detection (not for League, which reports its own multi-kills), the pacer and the route. Then she does one of three things:
  - **Model:** `ChatPanel.RequestGameLine` sends one request to the cloud AI API. The request is her persona prompt, then her game style and the fair-play rules, then the closed 18+ rule. It uses no memory and adds nothing to the history, and private mode refuses it. The reply is cleaned to one spoken line (`CleanReply`). A failed, empty or late reply becomes a pre-written line.
  - **Pre-written:** said as it is.
  - **Nothing:** cooldown, muted, or a minor moment.
- **Saying it:** `ChatPanel.SayGameLine` shows the line in the chat, plays the face and gesture, and speaks it.
  - A model line uses the cloud voice route.
  - A pre-written line always uses the local voice, so nothing about the game leaves this PC.
  - While she is answering the user, the game waits.
- **Her voice** is read from the active persona's Pisara sliders and relationship role each time.

## The Games section (JUH-47)

Settings → **Games**:
- **Game companion** on or off. It is off by default, and nothing is read while it is off. In private mode and with 18+ open it keeps running with pre-written lines only, and the section says so.
- **Let the cloud AI write her lines.** This is the game's own cloud consent. Without it, she uses pre-written lines only.
- **Her style in games:** her voice, shown so the user can see where it comes from.
- **How often:** Rare, Normal or Chatty, with the cooldown and the hourly cap spelled out.
- **Quiet for 10 minutes** and **Talk again.**
- **Counter-Strike 2** and **Dota 2:**
  - a **Watch** switch and whether the game is set up
  - **Set up** or **Set up again**, and **Remove** (JUH-146)
  - Dota 2 also shows the launch-option step
- **League of Legends:** a **Watch** switch, and whether a match is on.
- **Status:** the last thing that happened, plus the port for CS2 and Dota 2.

Settings saved before this have no game fields, and they load with everything off. The new keys are `gameCompanion`, `gameCloudConsent`, `gameFrequency`, `gameCs2`, `gameDota2`, `gameLeague`, `gsiPort` and `gsiToken`. The token is made on the first **Set up**, and it is letters and digits only.

## Gacha games (JUH-147)

Genshin Impact, Honkai: Star Rail, Zenless Zone Zero, Arknights: Endfield and Neverness to Everness have no official data feed. They all ban third-party tools that touch the game: HoYoverse bans for third-party software, and Endfield uses ACE anti-cheat. So Happi never reads the game's memory or touches the game process. She has two sources, both decided by Jussi on 2026-11-21:

- **Screen looks (all five games).**
  - `GachaGames.Match(process, title)` finds the game window, checking every 3 seconds. The process name wins, and a title must match exactly, so a browser tab about the game doesn't count. The Endfield and NTE names are best guesses, so their window titles back them up.
  - `GachaLookSchedule` decides when to look:
    - **every 2 minutes** while the game is the window in front, after 20 seconds there, so a quick alt-tab is not a look
    - on **Ctrl+Shift+G**, read with `GetAsyncKeyState` so it works while the game has focus
    - never twice within 10 seconds
  - The window is captured with Windows Graphics Capture, the way OBS does. It is downscaled to 768 px, sent once as a JPEG with her persona, her game style, the fair-play rules and `GachaLookPrompt.Rules`, then wiped. She says one short line, or `SILENT` and nothing.
  - **It needs all of these:**
    - the Games section's cloud consent
    - its own switch, **Let her look at the game**, which is off by default
    - xAI (Grok) as the cloud AI API, because images go only to xAI
  - It never runs in private mode or with 18+ on.
- **Pull history (Genshin, Star Rail, ZZZ, JUH-148).** It is opt-in: **React to my pulls**.
  - **The link.** When you open the pull history in the game, the game's built-in browser caches a link with a short-lived, read-only `authkey`. paimon.moe, Star Rail Warp Export and stardb use the same link.
  - **Finding it** (`GachaCacheFiles`, read only):
    1. The game's Unity log under `AppData\LocalLow` names its data folder. Genshin uses `miHoYo\Genshin Impact\output_log.txt`, Star Rail uses `Cognosphere\Star Rail\Player.log`, and ZZZ uses `miHoYo\ZenlessZoneZero\Player.log`; the CN names are tried too.
    2. The link is in `<data>\webCaches\<version>\Cache\Cache_Data\data_2`. The newest version is tried first, and the file is opened shared because the game may have it open.
  - **Where the link goes.** `GachaHistoryLink` keeps the newest link that carries an authkey and names this game. It sends the authkey only to `*.hoyoverse.com` or `*.mihoyo.com`, using the cached API link's own endpoint or the game's default (global or CN). It keeps only the auth parameters. The link is never logged or saved.
  - **Polling:** one banner's newest page (20 pulls) every 15 seconds, in turn, while the game window is open.
    - Genshin: 301, 302, 200, 500
    - Star Rail: 11, 12, 1
    - ZZZ: 2001, 3001, 1001, 5001 (as `real_gacha_type`)
    - Too many requests (`-110`) waits a minute. An expired link (`-101`) waits until the cache has a newer one, and the section asks you to open the history once.
  - **Events** (`GachaBannerTracker`):
    - The first page sets the baseline and the pity, which is the pulls since the last top-rarity result. Past the first page the pity is unknown, and then it isn't stated.
    - Each new top pull (5-star, or S-rank in ZZZ) is a big moment, with its name and pity.
    - Ten or more pulls without one is a normal moment. A single pull without one isn't spoken.
  - **Her lines** follow the mode: the model first, a pre-written line as the fallback ("Gold! Okay, your luck is better than your aim."), and pre-written lines only in private mode.
  - **Endfield and NTE: no.** Endfield's history needs the account's login token, which unlocks more than the history (one Endfield tracker turned out to be malware). NTE's exporter works by capturing the game's network traffic.

## Still to build

- A quick mute on her wheel.
- By hand on Windows: whether capture works when a HoYoverse game runs as administrator (the picture may be black), and whether Ctrl+Shift+G reaches Happi while such a game has focus.
- Pinning Riot's root certificate for the League poller.
- Game reaction animations as Jussi makes them (JUH-50). Today's lines use the faces and the Laugh, Nod, Shrug and Wave gestures.
- The old settings panel (the IMGUI fallback) has no Games section. The new settings view is the one with it.

## Open decisions for Jussi

None for the engine right now.
