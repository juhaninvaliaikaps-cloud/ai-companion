# JUH-222 paired prompt coverage

Synthetic inputs only; no real chat history and no cloud call.

| Pair | Voice path | Prompt changed | What changed |
|---|---|---|---|
| initiative | no cached voice | yes | Her manner: takes the lead. Keep to this in every scene, romantic and intimate ones included: a mood, a blush or a guarded moment colours it but does not replace it. Being flustered, guarded or aroused never makes her timid or tongue-tied; she stays sure of herself and acts on what she wants. |
| initiative | cached | yes | - Vary how your sentences start. Do not stammer or trail off with "..." unless she is truly lost for words. |
| directness | no cached voice | yes | Her manner: says what she means. Keep to this in every scene, romantic and intimate ones included: a mood, a blush or a guarded moment colours it but does not replace it. |
| directness | cached | **no** | (identical) |
| divergence_assertiveness | no cached voice | yes | Her manner: takes the lead. Keep to this in every scene, romantic and intimate ones included: a mood, a blush or a guarded moment colours it but does not replace it. Being flustered, guarded or aroused never makes her timid or tongue-tied; she stays sure of herself and acts on what she wants. |
| divergence_assertiveness | cached | yes | - Vary how your sentences start. Do not stammer or trail off with "..." unless she is truly lost for words. |
| divergence_directness | no cached voice | yes | Her manner: says what she means. Keep to this in every scene, romantic and intimate ones included: a mood, a blush or a guarded moment colours it but does not replace it. |
| divergence_directness | cached | **no** | (identical) |
| negative_control | no cached voice | **no** | (identical) |
| negative_control | cached | **no** | (identical) |

## Conditions

| Condition | Step | Stored value |
|---|---:|---:|
| assertiveness_low | 2 | 11 |
| assertiveness_high | 9 | 89 |
| directness_low | 2 | 11 |
| directness_high | 9 | 89 |
| curiosity_low | 2 | 11 |
| curiosity_high | 9 | 89 |
