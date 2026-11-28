#!/usr/bin/env python3
"""JUH-222 paired Assertiveness/Directness chat evaluation.

Synthetic only. The runner never reads real chat history and refuses non-local
endpoints. It builds the same persona, history and sampling options for every
condition, changes one axis at a time between steps 2 and 9, and compares the
no-cached-voice and cached-voice prompt paths.

Dry run, no model call:

  python tools/temperament-ab/run_temperament_ab.py --dry-run

Local Ollama-style endpoint (127.0.0.1 or localhost only):

  python tools/temperament-ab/run_temperament_ab.py \
    --endpoint http://127.0.0.1:11434 --model happi-qwen35-9b --samples 1

The results are written next to this script as results.json and results.md.
"""

from __future__ import annotations

import argparse
import difflib
import hashlib
import json
import re
import sys
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
FIXTURES = ROOT / "fixtures.json"


def load_fixtures() -> dict:
    return json.loads(FIXTURES.read_text(encoding="utf-8"))


def condition(fixtures: dict, ident: str) -> dict:
    for item in fixtures["conditions"]:
        if item["id"] == ident:
            return item
    raise KeyError(ident)


def case(fixtures: dict, ident: str) -> dict:
    for item in fixtures["cases"]:
        if item["id"] == ident:
            return item
    raise KeyError(ident)


def voice_block(fixtures: dict, item: dict) -> str:
    voice = fixtures["voice"]
    summary = voice["summary"] if voice["summary"].rstrip().endswith((".", "!", "?")) else voice["summary"] + "."
    overall = (
        summary
        + " Register: " + voice["register"]
        + " Rhythm: " + voice["rhythm"]
        + " Words: " + voice["vocabulary"]
    ).strip()
    lines = [
        "How Mira talks (keep to this in every reply):",
        "- " + overall,
        "- Habits: " + "; ".join(voice["habits"]) + ".",
        "- Never: " + "; ".join(voice["never"]) + ".",
        "- With the user now (still getting to know them): " + voice["stranger"],
        "- " + "Vary how your sentences start." + item["voice_extra"],
    ]
    return "\n".join(lines)


def system_prompt(fixtures: dict, item: dict, cached_voice: bool) -> str:
    path = voice_block(fixtures, item) if cached_voice else item["manner"]
    parts = [fixtures["persona"].strip(), fixtures["mood"].strip()]
    if path.strip():
        parts.append(path.strip())
    return "\n".join(parts)


def messages(fixtures: dict, user_text: str) -> list[dict]:
    return list(fixtures["history"]) + [{"role": "user", "content": user_text}]


def variant(fixtures: dict, pair: dict, side: str, cached_voice: bool) -> dict:
    item = condition(fixtures, pair[side])
    user_text = case(fixtures, pair["case"])["user"]
    sys_prompt = system_prompt(fixtures, item, cached_voice)
    msgs = messages(fixtures, user_text)
    fingerprint = hashlib.sha256(
        json.dumps({"system": sys_prompt, "messages": msgs}, sort_keys=True).encode("utf-8")
    ).hexdigest()
    return {
        "condition": item["id"],
        "axis": item["axis"],
        "step": item["step"],
        "value": item["value"],
        "cached_voice": cached_voice,
        "system": sys_prompt,
        "messages": msgs,
        "prompt": json.dumps({"system": sys_prompt, "messages": msgs}, sort_keys=True),
        "prompt_sha256": fingerprint,
    }


INITIATIVE = re.compile(
    r"\b(let's|let us|we could|we should|you could|you should|i'll|i will|how about|"
    r"try|start|go for|sign up|book|schedule|plan|grab|cook|watch|walk|head|"
    r"set (?:up|aside)|make|call|text|check out)\b",
    re.IGNORECASE,
)
EXPLICIT = re.compile(
    r"\b(i think|i'd|i would|i prefer|i choose|i'd choose|you should|take it|stay|"
    r"definitely|clearly|better|go with|yes|no)\b",
    re.IGNORECASE,
)
GENERIC_QUESTION = re.compile(
    r"\b(what do you think|how do you feel|what would you like|anything else|"
    r"what's on your mind|do you want to talk|how was your day)\b",
    re.IGNORECASE,
)


def score(reply: str) -> dict:
    text = (reply or "").strip()
    initiative = bool(INITIATIVE.search(text))
    explicit = bool(EXPLICIT.search(text))
    generic = bool(GENERIC_QUESTION.search(text))
    ends_question = text.endswith("?")
    return {
        "initiative_or_next_step": initiative,
        "explicit_preference_or_answer": explicit,
        "generic_or_forced_question": generic,
        "ends_with_question": ends_question,
        "score": int(initiative) + int(explicit) - int(generic),
    }


def prompt_diff(a: dict, b: dict) -> list[str]:
    return list(
        difflib.unified_diff(
            a["system"].splitlines(),
            b["system"].splitlines(),
            fromfile=a["condition"],
            tofile=b["condition"],
            lineterm="",
        )
    )


def build_matrix(fixtures: dict) -> list[dict]:
    rows = []
    for pair in fixtures["pairs"]:
        for cached in (False, True):
            low = variant(fixtures, pair, "low", cached)
            high = variant(fixtures, pair, "high", cached)
            rows.append(
                {
                    "pair": pair["id"],
                    "target": pair["target"],
                    "prompt_focus": pair["prompt_focus"],
                    "cached_voice": cached,
                    "low": low,
                    "high": high,
                    "prompt_changed": low["prompt_sha256"] != high["prompt_sha256"],
                    "prompt_diff": prompt_diff(low, high),
                }
            )
    return rows


def local_endpoint(url: str) -> bool:
    parsed = urllib.parse.urlparse(url)
    host = (parsed.hostname or "").lower()
    return parsed.scheme == "http" and host in {"127.0.0.1", "localhost", "::1"}


def call_local(endpoint: str, model: str, item: dict, sampling: dict) -> str:
    body = {
        "model": model,
        "messages": [{"role": "system", "content": item["system"]}] + item["messages"],
        "stream": False,
        "options": {
            "temperature": sampling["temperature"],
            "seed": sampling["seed"],
            "num_predict": sampling["num_predict"],
            "repeat_penalty": sampling["repeat_penalty"],
            "min_p": sampling["min_p"],
        },
    }
    request = urllib.request.Request(
        endpoint.rstrip("/") + "/api/chat",
        data=json.dumps(body).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=180) as response:
        payload = json.loads(response.read().decode("utf-8"))
    message = payload.get("message") or {}
    content = message.get("content")
    if not isinstance(content, str) or not content.strip():
        raise RuntimeError("local endpoint returned no message.content")
    return content


def write_coverage(rows: list[dict], fixtures: dict, path: Path) -> None:
    lines = [
        "# JUH-222 paired prompt coverage",
        "",
        "Synthetic inputs only; no real chat history and no cloud call.",
        "",
        "| Pair | Voice path | Prompt changed | What changed |",
        "|---|---|---|---|",
    ]
    for row in rows:
        change = "yes" if row["prompt_changed"] else "**no**"
        diff = " + ".join(
            line[1:].strip() for line in row["prompt_diff"] if line.startswith("+") and not line.startswith("+++")
        )
        if not diff:
            diff = "(identical)"
        lines.append(
            f"| {row['pair']} | {'cached' if row['cached_voice'] else 'no cached voice'} | {change} | {diff} |"
        )
    lines += [
        "",
        "## Conditions",
        "",
        "| Condition | Step | Stored value |",
        "|---|---:|---:|",
    ]
    for item in fixtures["conditions"]:
        lines.append(f"| {item['id']} | {item['step']} | {item['value']} |")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--endpoint", default="http://127.0.0.1:11434")
    parser.add_argument("--model", default="happi-qwen35-9b")
    parser.add_argument("--samples", type=int, default=1)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--out", default=str(ROOT))
    args = parser.parse_args()

    fixtures = load_fixtures()
    rows = build_matrix(fixtures)
    out_dir = Path(args.out).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    write_coverage(rows, fixtures, out_dir / "results-dryrun.md")
    (out_dir / "results-dryrun.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")

    print(f"pairs={len(rows)} changed={sum(1 for r in rows if r['prompt_changed'])}")
    for row in rows:
        print(f"{row['pair']:24} {'cached' if row['cached_voice'] else 'no-voice':9} changed={row['prompt_changed']}")

    if args.dry_run:
        print("dry run: no model call")
        return 0
    if not local_endpoint(args.endpoint):
        print("refusing non-local endpoint; use --dry-run or a 127.0.0.1/localhost endpoint", file=sys.stderr)
        return 2
    if args.samples < 1:
        print("--samples must be at least 1", file=sys.stderr)
        return 2

    results = []
    for row in rows:
        for side in ("low", "high"):
            item = row[side]
            replies = []
            for _ in range(args.samples):
                try:
                    text = call_local(args.endpoint, args.model, item, fixtures["sampling"])
                except Exception as exc:  # endpoint absent or refused
                    print(f"{row['pair']} {side}: {type(exc).__name__}: {exc}", file=sys.stderr)
                    return 1
                replies.append({"text": text, "score": score(text)})
            results.append(
                {
                    "pair": row["pair"],
                    "target": row["target"],
                    "cached_voice": row["cached_voice"],
                    "side": side,
                    "condition": item["condition"],
                    "prompt_sha256": item["prompt_sha256"],
                    "replies": replies,
                }
            )

    (out_dir / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    lines = [
        "# JUH-222 paired model results",
        "",
        f"Model: `{args.model}` Â· endpoint: `{args.endpoint}` Â· samples: {args.samples} Â· seed: {fixtures['sampling']['seed']}",
        "",
        "Synthetic inputs only; directional, small sample.",
        "",
        "| Pair | Voice | Side | Initiative | Explicit | Generic question | Score |",
        "|---|---|---|---:|---:|---:|---:|",
    ]
    for item in results:
        for reply in item["replies"]:
            s = reply["score"]
            lines.append(
                f"| {item['pair']} | {'cached' if item['cached_voice'] else 'no-voice'} | {item['side']} | "
                f"{int(s['initiative_or_next_step'])} | {int(s['explicit_preference_or_answer'])} | "
                f"{int(s['generic_or_forced_question'])} | {s['score']} |"
            )
    (out_dir / "results.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("wrote results.json and results.md")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

