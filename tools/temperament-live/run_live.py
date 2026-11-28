#!/usr/bin/env python3
"""JUH-236: does each temperament slider make a difference you can notice in chat?

Synthetic only. Every prompt is a made-up persona, a made-up two-message history and a
made-up user message (fixtures.json), with the 18+ gate closed. Nothing reads real chat
history, memories or secrets, and no API key is printed or written anywhere.

The slider text comes from the production code: PromptLines (a small .NET tool that
compiles Assets/_Project/Scripts/Core) prints PersonaManner.Line, PersonaManner.ConversationLine
and VoicePrompt.Block for each setting, so this runner never copies those strings.

For each model, slider and voice path (no cached voice / cached voice), it samples low
(step 2) and high (step 9) with the same seeds, plus a negative control: the neutral
profile (every slider at step 5) sampled twice with two seed sets. The control's gap is the
noise floor. A blind judge (the cloud model, temperature 0) answers one yes/no question per
reply about the observable behaviour; it never sees the condition.

  python tools/temperament-live/run_live.py --models local,cloud --label baseline

Local: Ollama on 127.0.0.1 only. Cloud: api.deepseek.com only, in public mode (no private
or 18+ content is ever in these prompts). The cloud key is the app's own (XAI_API_KEY, or
%APPDATA%\\ProjectLeia\\secrets.json unprotected with Windows DPAPI in memory).
"""

from __future__ import annotations

import argparse
import base64
import concurrent.futures
import ctypes
import hashlib
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
FIXTURES = ROOT / "fixtures.json"
TOOL = ROOT / "PromptLines"
LOCAL_HOSTS = {"127.0.0.1", "localhost", "::1"}
CLOUD_HOST = "api.deepseek.com"


# ---- HTTP without proxies or redirects --------------------------------------------------------

class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):  # noqa: D401
        raise urllib.error.HTTPError(req.full_url, code, "redirects are refused", headers, fp)


OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}), _NoRedirect())


def post_json(url: str, body: dict, headers: dict, timeout: float) -> dict:
    request = urllib.request.Request(url, data=json.dumps(body).encode("utf-8"), method="POST")
    request.add_header("Content-Type", "application/json")
    for key, value in headers.items():
        request.add_header(key, value)
    with OPENER.open(request, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def check_local(url: str) -> None:
    parts = urllib.parse.urlsplit(url)
    if parts.scheme != "http" or parts.hostname not in LOCAL_HOSTS:
        raise SystemExit("The local endpoint must be http://127.0.0.1 or localhost.")


def check_cloud(url: str) -> None:
    parts = urllib.parse.urlsplit(url)
    if parts.scheme != "https" or parts.hostname != CLOUD_HOST:
        raise SystemExit("The cloud endpoint must be https://" + CLOUD_HOST + ".")


# ---- The app's cloud key, in memory only ---------------------------------------------------------

class _Blob(ctypes.Structure):
    _fields_ = [("cbData", ctypes.c_uint32), ("pbData", ctypes.POINTER(ctypes.c_char))]


def _unprotect(data: bytes) -> bytes:
    buffer = ctypes.create_string_buffer(data, len(data))
    incoming = _Blob(len(data), ctypes.cast(buffer, ctypes.POINTER(ctypes.c_char)))
    outgoing = _Blob()
    if not ctypes.windll.crypt32.CryptUnprotectData(ctypes.byref(incoming), None, None, None, None, 0x1, ctypes.byref(outgoing)):
        raise SystemExit("Windows could not unprotect the app's API key.")
    try:
        return ctypes.string_at(outgoing.pbData, outgoing.cbData)
    finally:
        ctypes.windll.kernel32.LocalFree(outgoing.pbData)


def cloud_key() -> str:
    """The same order as the app's ApiKeyResolver: the environment variable, then secrets.json."""
    env = (os.environ.get("XAI_API_KEY") or "").strip()
    if env:
        return env
    path = Path(os.environ.get("APPDATA", "")) / "ProjectLeia" / "secrets.json"
    if not path.is_file():
        raise SystemExit("No cloud key: set XAI_API_KEY or save one in the app.")
    data = json.loads(path.read_text(encoding="utf-8"))
    value = data.get("xaiApiKey") or ""
    if data.get("protected"):
        value = _unprotect(base64.b64decode(value)).decode("utf-8")
    value = value.strip()
    if not value:
        raise SystemExit("The app's cloud key is empty.")
    return value


# ---- Prompts ------------------------------------------------------------------------------------

def prompt_lines(keys: list[str]) -> dict:
    command = ["dotnet", "run", "--project", str(TOOL), "--no-build", "--", "--voice", str(FIXTURES)] + keys
    out = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", check=True).stdout
    return {item["key"]: item for item in json.loads(out)}


def system_prompt(fx: dict, lines: dict, cached_voice: bool) -> str:
    """The order ChatPanel.PersonaPrompt uses for an ordinary chat turn."""
    body = fx["persona"].strip() + "\n" + fx["mood"].strip()
    if not cached_voice and lines["manner"]:
        body += "\n" + lines["manner"]          # PersonaMannerNow.Append, only before a voice reading
    body += "\n\n" + fx["relationship"].strip()  # RelationshipNow.Append
    if cached_voice and lines["voice"]:
        body += "\n\n" + lines["voice"]         # VoiceNow.Append
    if lines["conversation"]:
        body += "\n\n" + lines["conversation"]  # PersonaMannerNow.AppendConversation
    return body


def messages(fx: dict, system: str, user: str) -> list[dict]:
    return [{"role": "system", "content": system}] + fx["history"] + [{"role": "user", "content": user}]


# ---- Models -------------------------------------------------------------------------------------

def ask_local(args, fx: dict, system: str, user: str, seed: int) -> str:
    s = fx["sampling"]
    body = {
        "model": args.local_model,
        "stream": False,
        "messages": messages(fx, system, user),
        # The app's ordinary chat sets Roleplay, which adds OllamaChatJson.RoleplayOptions.
        "options": {"num_ctx": s["num_ctx"], "temperature": s["temperature"], "num_predict": s["max_tokens"], "seed": seed,
                    "repeat_penalty": 1.08, "repeat_last_n": 1024, "min_p": 0.05},
    }
    if args.local_think == "false":
        body["think"] = False
    reply = post_json(args.local_endpoint.rstrip("/") + "/api/chat", body, {}, 300)
    return (reply.get("message") or {}).get("content", "").strip()


def ask_cloud(args, fx: dict, key: str, system: str, user: str, seed: int) -> str:
    s = fx["sampling"]
    body = {
        "model": args.cloud_model,
        "stream": False,
        "messages": messages(fx, system, user),
        "temperature": s["temperature"],
        "max_tokens": s["max_tokens"],
        "thinking": {"type": "disabled"},  # as CloudChatProvider does for DeepSeek
    }
    for attempt in range(3):
        try:
            reply = post_json(args.cloud_endpoint.rstrip("/") + "/chat/completions", body, {"Authorization": "Bearer " + key}, 120)
            return reply["choices"][0]["message"]["content"].strip()
        except urllib.error.HTTPError as error:
            if error.code not in (429, 500, 502, 503) or attempt == 2:
                raise SystemExit("Cloud request failed: HTTP " + str(error.code))
            time.sleep(3 * (attempt + 1))
    return ""


JUDGE_SYSTEM = (
    "You score one reply from a chat companion called Mira. You are given the conversation so far, the user's "
    "last message, her reply, and one yes/no question about the reply. Answer the question about the reply only, "
    "strictly and literally. Reply with JSON only: {\"answer\": \"yes\" or \"no\", \"reason\": \"<at most 15 words>\"}."
)


def judge(args, key: str, fx: dict, question: str, user: str, reply: str) -> tuple[str, str]:
    history = "\n".join(m["role"] + ": " + m["content"] for m in fx["history"])
    content = (
        "Conversation so far:\n" + history + "\n\nUser's last message:\n" + user
        + "\n\nMira's reply:\n" + (reply or "(empty)") + "\n\nQuestion: " + question
    )
    body = {
        "model": args.cloud_model,
        "stream": False,
        "temperature": 0,
        "max_tokens": 80,
        "thinking": {"type": "disabled"},
        "response_format": {"type": "json_object"},
        "messages": [{"role": "system", "content": JUDGE_SYSTEM}, {"role": "user", "content": content}],
    }
    for attempt in range(3):
        try:
            out = post_json(args.cloud_endpoint.rstrip("/") + "/chat/completions", body, {"Authorization": "Bearer " + key}, 120)
            parsed = json.loads(out["choices"][0]["message"]["content"])
            answer = str(parsed.get("answer", "")).strip().lower()
            return ("yes" if answer.startswith("y") else "no"), str(parsed.get("reason", ""))[:160]
        except (urllib.error.HTTPError, json.JSONDecodeError, KeyError):
            if attempt == 2:
                return "error", "judge failed"
            time.sleep(3 * (attempt + 1))
    return "error", "judge failed"


# ---- The matrix ---------------------------------------------------------------------------------

def matrix(fx: dict, axes: list[str]) -> list[dict]:
    s = fx["sampling"]
    items = []
    for axis in axes:
        spec = fx["axes"][axis]
        for cached in (False, True):
            for side, key, seeds in (("low", axis + ":2", s["seeds"]), ("high", axis + ":9", s["seeds"]),
                                     ("controlA", "none", s["seeds"]), ("controlB", "none", s["control_seeds"])):
                for case_index, user in enumerate(spec["cases"]):
                    for seed in seeds:
                        items.append({"axis": axis, "cached_voice": cached, "side": side, "lines_key": key,
                                      "case": case_index, "user": user, "seed": seed})
    return items


def rate(rows: list[dict]) -> float | None:
    scored = [r for r in rows if r["judge"] in ("yes", "no")]
    if not scored:
        return None
    return sum(1 for r in scored if r["judge"] == "yes") / len(scored)


def verdict(low: float | None, high: float | None, noise: float | None) -> str:
    if low is None or high is None:
        return "not measured"
    gap = high - low
    floor = noise or 0.0
    if gap >= 0.5 and gap > floor:
        return "noticeable"
    if gap >= 0.33 and gap > floor:
        return "weak"
    return "not noticeable"


def summarize(rows: list[dict], prompts: dict) -> list[dict]:
    out = []
    keys = sorted({(r["model"], r["axis"], r["cached_voice"]) for r in rows}, key=lambda k: (k[0], k[1], k[2]))
    for model, axis, cached in keys:
        group = [r for r in rows if r["model"] == model and r["axis"] == axis and r["cached_voice"] == cached]
        part = {side: rate([r for r in group if r["side"] == side]) for side in ("low", "high", "controlA", "controlB")}
        noise = abs(part["controlA"] - part["controlB"]) if part["controlA"] is not None and part["controlB"] is not None else None
        differs = prompts[(axis, cached, "low")] != prompts[(axis, cached, "high")]
        out.append({
            "model": model, "axis": axis, "voice": "cached" if cached else "none",
            "low": part["low"], "high": part["high"], "neutral": part["controlA"], "noise": noise,
            "prompt_differs": differs,
            "verdict": verdict(part["low"], part["high"], noise) if differs else "prompt identical",
            "n": len([r for r in group if r["side"] == "low"]),
        })
    return out


def pct(value: float | None) -> str:
    return "-" if value is None else f"{round(value * 100):d}%"


def write_markdown(path: Path, label: str, args, summary: list[dict]) -> None:
    lines = [
        f"# JUH-236 run: {label}",
        "",
        f"Local `{args.local_model}` (think={args.local_think}), cloud `{args.cloud_model}`; judge `{args.cloud_model}` at temperature 0.",
        "Each cell is the share of replies where the judge answered yes. Low is step 2, high step 9, neutral every slider at step 5;",
        "noise is the gap between two neutral samples with different seeds.",
        "",
        "| Model | Slider | Voice | Low | High | Neutral | Noise | Prompt differs | Verdict | n |",
        "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |",
    ]
    for s in summary:
        lines.append(f"| {s['model']} | {s['axis']} | {s['voice']} | {pct(s['low'])} | {pct(s['high'])} | {pct(s['neutral'])} | "
                     f"{pct(s['noise'])} | {'yes' if s['prompt_differs'] else 'no'} | {s['verdict']} | {s['n']} |")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--models", default="local,cloud")
    parser.add_argument("--axes", default="Curiosity,Assertiveness,Directness,Openness,Playfulness")
    parser.add_argument("--label", default="run")
    parser.add_argument("--local-endpoint", default="http://127.0.0.1:11434")
    parser.add_argument("--local-model", default="happi-qwen35-9b")
    parser.add_argument("--local-think", choices=("omit", "false"), default="false",
                        help="omit sends no think field (what the app sends today); false turns thinking off")
    parser.add_argument("--cloud-endpoint", default="https://api.deepseek.com/v1")
    parser.add_argument("--cloud-model", default="deepseek-flash")
    parser.add_argument("--workers", type=int, default=6, help="parallel cloud requests")
    args = parser.parse_args()

    check_local(args.local_endpoint)
    check_cloud(args.cloud_endpoint)
    fx = json.loads(FIXTURES.read_text(encoding="utf-8"))
    axes = [a.strip() for a in args.axes.split(",") if a.strip()]
    models = [m.strip() for m in args.models.split(",") if m.strip()]
    key = cloud_key()  # the judge is always the cloud model

    subprocess.run(["dotnet", "build", str(TOOL), "-nologo", "-v", "q"], check=True, capture_output=True)
    keys = sorted({k for a in axes for k in (a + ":2", a + ":9")} | {"none"})
    lines = prompt_lines(keys)
    items = matrix(fx, axes)
    prompts = {}
    for axis in axes:
        for cached in (False, True):
            for side, k in (("low", axis + ":2"), ("high", axis + ":9")):
                # The local turn rule counts as part of the prompt for "prompt differs".
                prompts[(axis, cached, side)] = system_prompt(fx, lines[k], cached) + "\n[local turn] " + lines[k].get("local_turn", "")

    out_dir = ROOT / "results" / args.label
    out_dir.mkdir(parents=True, exist_ok=True)
    rows: list[dict] = []

    def one(model: str, item: dict) -> dict:
        system = system_prompt(fx, lines[item["lines_key"]], item["cached_voice"])
        if model == "local":
            # PersonaTurnRule: the local route's per-turn rule goes after the user's message, as
            # ChatConversation.AddTurnReminder appends the turn reminder. The judge sees the message alone.
            rule = lines[item["lines_key"]].get("local_turn", "")
            user = item["user"] + ("\n\n" + rule if rule else "")
            reply = ask_local(args, fx, system, user, item["seed"])
        else:
            reply = ask_cloud(args, fx, key, system, item["user"], item["seed"])
        answer, reason = judge(args, key, fx, fx["axes"][item["axis"]]["question"], item["user"], reply)
        row = dict(item)
        row.update({"model": model, "prompt_sha": hashlib.sha256(system.encode("utf-8")).hexdigest()[:12],
                    "reply": reply, "judge": answer, "judge_reason": reason})
        return row

    for model in models:
        started = time.time()
        if model == "local":
            for index, item in enumerate(items):
                rows.append(one(model, item))
                if index % 20 == 0:
                    print(f"local {index + 1}/{len(items)}", flush=True)
        else:
            with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, args.workers)) as pool:
                for index, row in enumerate(pool.map(lambda it: one(model, it), items)):
                    rows.append(row)
                    if index % 40 == 0:
                        print(f"cloud {index + 1}/{len(items)}", flush=True)
        print(f"{model}: {len(items)} replies in {time.time() - started:.0f} s", flush=True)

    summary = summarize(rows, prompts)
    (out_dir / "replies.jsonl").write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8")
    (out_dir / "prompts.json").write_text(json.dumps({f"{a}|{'cached' if c else 'none'}|{s}": p for (a, c, s), p in prompts.items()},
                                                     ensure_ascii=False, indent=1), encoding="utf-8")
    (out_dir / "summary.json").write_text(json.dumps(summary, indent=1), encoding="utf-8")
    write_markdown(out_dir / "summary.md", args.label, args, summary)
    print((out_dir / "summary.md").read_text(encoding="utf-8"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
