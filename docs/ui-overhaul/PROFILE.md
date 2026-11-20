# Profile panel (JUH-121)

Her profile: how she feels about you, her personality, her world, and what she remembers about you. It opens from the wheel's **Profile** slice (JUH-121, Jussi 2026-11-15).

Mockup: https://claude.ai/artifact/3Za6cHCRA24ozNUnTz6ceD. Jussi picked Feelings **option A, gauges** (2026-11-15).

| Tab | What it shows | Built by |
| --- | --- | --- |
| **Feelings** | The affection bar from −100 to +100. It fills from the middle toward her marker: pink when she likes you, grey when not. Then the familiarity bar, her mood now with her three strongest moods, and why the relationship moved recently (the last 4 reasons). | `Core/ProfileModel.cs` `ProfileFeelings` |
| **Personality** | The nine temperament sliders she runs on (JUH-136, Jussi 2026-11-20): Warmth, Volatility, Assertiveness, Playfulness, Openness, Energy, Directness, Confidence and Curiosity, each as a bar with its value and end word ("warm", "steady", "in between"). They come from her manual sliders, the reading (Auto), or her preset's point. Curiosity is greyed: it does not change her mood yet. The old 5-axis radar is only the fallback when no sliders are known. Then the reading's line and reason. While 18+ is on, it also shows Innocent to Perverted. Then **Her secrets** (JUH-123): "3 of 8 found", in two groups, What stings her and What wins her over. A found one shows its text, the rest are locked "Not found yet" chips, and the newest find has a gold outline. While 18+ is on, **Her 18+ secrets** (JUH-125) follows: Kinks, Sensitive areas, Shy to do, and how shy she is now. | `ProfilePersonality`, `ProfileSecrets`, `ProfileIntimateSecrets`, `RadarGraphic` |
| **Her world** | What she is doing right now, what she is waiting for (open threads), what happened while you were away, her last days, and her backstory (6 shown, "Show all"). | `Memory/ProfileWorld.cs` `ProfileWorld` |
| **Memories** | What she remembers about you, newest first (8 shown), and Manage in Settings. Her own life is on Her world, not here. | `ProfileMemories` |

The header shows her name and who she is to you, for example "Stepsister (family) · you share a past" (`ProfileHeader`).

The data is gathered when the panel or a tab opens (`HappiWheelHost.BuildProfileData`), never per frame. Nothing in the panel is sent anywhere.
