# JUH-236 run: after-lines

Local `happi-qwen35-9b` (think=false), cloud `deepseek-flash`; judge `deepseek-flash` at temperature 0.
Each cell is the share of replies where the judge answered yes. Low is step 2, high step 9, neutral every slider at step 5;
noise is the gap between two neutral samples with different seeds.

| Model | Slider | Voice | Low | High | Neutral | Noise | Prompt differs | Verdict | n |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| cloud | Openness | none | 33% | 100% | 100% | 0% | yes | noticeable | 6 |
| cloud | Openness | cached | 50% | 100% | 83% | 0% | yes | noticeable | 6 |
| cloud | Playfulness | none | 0% | 100% | 100% | 0% | yes | noticeable | 6 |
| cloud | Playfulness | cached | 83% | 100% | 100% | 17% | yes | not noticeable | 6 |
| local | Openness | none | 67% | 100% | 100% | 17% | yes | weak | 6 |
| local | Openness | cached | 67% | 100% | 83% | 0% | yes | weak | 6 |
| local | Playfulness | none | 67% | 100% | 50% | 17% | yes | weak | 6 |
| local | Playfulness | cached | 67% | 100% | 100% | 33% | yes | not noticeable | 6 |
