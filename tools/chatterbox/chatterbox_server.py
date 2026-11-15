"""Chatterbox Turbo server for Happi.

Loads the model once, then answers one JSON request per stdin line with one JSON
reply on stdout. A preset voice is a folder under --voices containing
reference.wav; the conditionals are cached per voice.

Request : {"text": "...", "voice": "af_nicole", "speed": 1.0}
Response: {"ok": true, "wav": "<base64 WAV>"} or {"ok": false, "error": "..."}
"""
import argparse
import base64
import contextlib
import io
import json
import os
import re
import sys

import soundfile as sf
import torch
from chatterbox.tts_turbo import ChatterboxTurboTTS

SAFE = re.compile(r"^[a-z0-9_-]{1,64}$")


def pick_device(requested):
    wanted = (requested or "auto").strip().lower()
    if wanted == "cuda" and not torch.cuda.is_available():
        sys.stderr.write("chatterbox: CUDA not available, using CPU\n")
        sys.stderr.flush()
        return "cpu"
    if wanted == "auto":
        return "cuda" if torch.cuda.is_available() else "cpu"
    return wanted if wanted in ("cpu", "cuda") else "cpu"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--voices", required=True)
    parser.add_argument("--device", default="auto")
    args = parser.parse_args()

    device = pick_device(args.device)
    root = os.path.abspath(args.voices)
    # The model loader and Perth may print to stdout; reserve stdout for JSON replies.
    with contextlib.redirect_stdout(sys.stderr):
        model = ChatterboxTurboTTS.from_pretrained(device=device)
    cache = {}
    current = None
    sys.stderr.write("chatterbox: ready device=%s voices=%s\n" % (device, root))
    sys.stderr.flush()

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            request = json.loads(line)
            text = (request.get("text") or "").strip()
            if not text:
                raise ValueError("empty text")
            voice = (request.get("voice") or "").strip().lower()
            if not SAFE.match(voice):
                raise ValueError("invalid voice id")
            speed = float(request.get("speed") or 1.0)
            reference = os.path.join(root, voice, "reference.wav")
            if not os.path.isfile(reference):
                raise ValueError("missing reference for %s" % voice)

            if current != voice:
                conds = cache.get(voice)
                if conds is None:
                    model.prepare_conditionals(reference)
                    conds = model.conds
                    cache[voice] = conds
                else:
                    model.conds = conds
                current = voice

            wav = model.generate(text)
            if speed != 1.0 and speed > 0:
                wav = wav  # Chatterbox has no speed control; keep the request shape.
            buffer = io.BytesIO()
            sf.write(buffer, wav.squeeze(0).numpy(), model.sr, format="WAV")
            payload = base64.b64encode(buffer.getvalue()).decode("ascii")
            reply = {"ok": True, "wav": payload}
        except Exception as error:  # noqa: BLE001 - report to the app, never kill the loop
            reply = {"ok": False, "error": "%s: %s" % (type(error).__name__, error)}

        sys.stdout.write(json.dumps(reply) + "\n")
        sys.stdout.flush()


if __name__ == "__main__":
    main()
