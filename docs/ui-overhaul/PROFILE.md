# Profile panel (JUH-121)

Her profile: how she feels about you, her personality, her world, and what she remembers about you. It opens from the wheel's **Profile** slice (JUH-121, Jussi 2026-11-15).

Mockup: https://claude.ai/artifact/3Za6cHCRA24ozNUnTz6ceD. Jussi picked Feelings **option A, gauges** (2026-11-15).

| Tab | What it shows | Built by |
| --- | --- | --- |
| **Feelings** | The affection bar from −100 to +100. It fills from the middle toward her marker: pink when she likes you, grey when not. Then the familiarity bar, her mood now with her three strongest moods, and why the relationship moved recently (the last 4 reasons). | `Core/ProfileModel.cs` `ProfileFeelings` |
| **Personality** | The nine temperament sliders she runs on (JUH-136, Jussi 2026-11-20): Warmth, Volatility, Assertiveness, Playfulness, Openness, Energy, Directness, Confidence and Curiosity, each as a bar with its value and end word ("warm", "steady", "in between"). They come from her manual sliders, the reading (Auto), or her preset's point. Curiosity is greyed: it does not change her mood yet. The old 5-axis radar is only the fallback when no sliders are known. Then the reading's line and reason. While 18+ is on, it also shows Innocent to Perverted. Then **Her secrets** (JUH-123): "3 of 8 found", in two groups, What stings her and What wins her over. A found one shows its text, the rest are locked "Not found yet" chips, and the newest find has a gold outline. While 18+ is on, **Her 18+ secrets** (JUH-125) follows: Kinks, Sensitive areas, Shy to do, and how shy she is now. | `ProfilePersonality`, `ProfileSecrets`, `ProfileIntimateSecrets`, `RadarGraphic` |
| **Her world** | What she is doing right now, what she is waiting for (open threads), what happened while you were away, her last days, and her backstory (6 shown, "Show all"). | `Memory/ProfileWorld.cs` `ProfileWorld` |
| **Memories** | What she remembers about you, newest first (8 shown), and Manage in Settings. Her own life is on Her world, not here. | `ProfileMemories` |

### Ribbon preview

Launch with `--ribbon-ui`. Profile uses the charcoal frame, the full-width Happi-Mood banner, and cyan tabs. The banner keeps its original proportions with Close in the upper-right corner. The frame fits the visible work area, and the tabs use a second row when needed. Both the Ribbon and radial wheel open the same Profile view. Ctrl+Tab and Ctrl+Shift+Tab cycle through the tabs. Page Up and Page Down scroll the current tab, and Home and End jump to its edges. Each tab keeps its reading position while Profile stays open.

- Feelings starts with her current mood, then shows the relationship and recent changes.
- Personality shows an icon, ten numbered steps, and a description for each trait. Reached steps turn from cyan to pink at 8, and the current step is taller. Edit temperament opens the same tracks as controls. Click a step or focus a track and use Left and Right. Up and Down move between tracks. Discoveries and gated details keep their existing sections.
- Her world puts current activity, open threads, events while away, recent days, and backstory in distinct cards.
- Memories separates what she remembers about you from the relationship timeline. Manage memories opens the Memories settings directly.

The classic header shows her name and who she is to you, for example "Stepsister (family) · you share a past" (`ProfileHeader`).

The data is gathered when the panel or a tab opens (`HappiWheelHost.BuildProfileData`), never per frame. Nothing in the panel is sent anywhere.
