# JUH-236 run: baseline

Local `happi-qwen35-9b` (think=false), cloud `deepseek-flash`; judge `deepseek-flash` at temperature 0.
Each cell is the share of replies where the judge answered yes. Low is step 2, high step 9, neutral every slider at step 5;
noise is the gap between two neutral samples with different seeds.

| Model | Slider | Voice | Low | High | Neutral | Noise | Prompt differs | Verdict | n |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| cloud | Assertiveness | none | 50% | 100% | 100% | 0% | yes | noticeable | 6 |
| cloud | Assertiveness | cached | 50% | 83% | 50% | 0% | yes | weak | 6 |
| cloud | Curiosity | none | 100% | 100% | 83% | 17% | yes | not noticeable | 6 |
| cloud | Curiosity | cached | 83% | 100% | 83% | 0% | yes | not noticeable | 6 |
| cloud | Directness | none | 17% | 100% | 83% | 17% | yes | noticeable | 6 |
| cloud | Directness | cached | 83% | 100% | 67% | 17% | yes | not noticeable | 6 |
| cloud | Openness | none | 50% | 100% | 100% | 0% | yes | noticeable | 6 |
| cloud | Openness | cached | 83% | 100% | 100% | 17% | no | prompt identical | 6 |
| cloud | Playfulness | none | 0% | 100% | 100% | 0% | yes | noticeable | 6 |
| cloud | Playfulness | cached | 83% | 83% | 67% | 33% | no | prompt identical | 6 |
| local | Assertiveness | none | 50% | 83% | 83% | 17% | yes | weak | 6 |
| local | Assertiveness | cached | 33% | 100% | 50% | 17% | yes | noticeable | 6 |
| local | Curiosity | none | 83% | 100% | 67% | 50% | yes | not noticeable | 6 |
| local | Curiosity | cached | 100% | 100% | 33% | 0% | yes | not noticeable | 6 |
| local | Directness | none | 0% | 83% | 83% | 17% | yes | noticeable | 6 |
| local | Directness | cached | 50% | 67% | 33% | 17% | yes | not noticeable | 6 |
| local | Openness | none | 100% | 100% | 100% | 0% | yes | not noticeable | 6 |
| local | Openness | cached | 83% | 83% | 83% | 0% | no | prompt identical | 6 |
| local | Playfulness | none | 83% | 100% | 50% | 17% | yes | not noticeable | 6 |
| local | Playfulness | cached | 100% | 100% | 100% | 33% | no | prompt identical | 6 |
