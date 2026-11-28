# JUH-236 run: after-turn-rule

Local `happi-qwen35-9b` (think=false), cloud `deepseek-flash`; judge `deepseek-flash` at temperature 0.
Each cell is the share of replies where the judge answered yes. Low is step 2, high step 9, neutral every slider at step 5;
noise is the gap between two neutral samples with different seeds.

| Model | Slider | Voice | Low | High | Neutral | Noise | Prompt differs | Verdict | n |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| cloud | Curiosity | none | 0% | 100% | 83% | 17% | yes | noticeable | 6 |
| cloud | Curiosity | cached | 33% | 100% | 83% | 17% | yes | noticeable | 6 |
| cloud | Openness | none | 50% | 100% | 100% | 0% | yes | noticeable | 6 |
| cloud | Openness | cached | 50% | 100% | 83% | 0% | yes | noticeable | 6 |
| cloud | Playfulness | none | 0% | 100% | 100% | 0% | yes | noticeable | 6 |
| cloud | Playfulness | cached | 17% | 100% | 67% | 33% | yes | noticeable | 6 |
| local | Curiosity | none | 0% | 100% | 67% | 50% | yes | noticeable | 6 |
| local | Curiosity | cached | 0% | 100% | 33% | 0% | yes | noticeable | 6 |
| local | Openness | none | 33% | 100% | 100% | 17% | yes | noticeable | 6 |
| local | Openness | cached | 0% | 100% | 83% | 0% | yes | noticeable | 6 |
| local | Playfulness | none | 0% | 100% | 50% | 17% | yes | noticeable | 6 |
| local | Playfulness | cached | 17% | 100% | 100% | 50% | yes | noticeable | 6 |
