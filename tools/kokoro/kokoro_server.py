"""Kokoro TTS server for the AI Companion.

Loads the model once and answers one request per line on stdin with one JSON line on
stdout, so the app never pays the model load cost per sentence.

Request : {"text": "...", "speed": 1.0}
Response: {"ok": true, "wav": "<base64 WAV>"}  or  {"ok": false, "error": "..."}
"""
import argparse
import base64
import io
import json
import sys

import soundfile as sf
from kokoro_onnx import Kokoro


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True)
    parser.add_argument("--voices", required=True)
    parser.add_argument("--voice", default="af_heart")
    parser.add_argument("--lang", default="en-us")
    args = parser.parse_args()

    kokoro = Kokoro(args.model, args.voices)
    sys.stderr.write("kokoro: ready\n")
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
            speed = float(request.get("speed") or 1.0)
            voice = request.get("voice") or args.voice

            samples, rate = kokoro.create(text, voice=voice, speed=speed, lang=args.lang)
            buffer = io.BytesIO()
            sf.write(buffer, samples, rate, format="WAV")
            payload = base64.b64encode(buffer.getvalue()).decode("ascii")
            reply = {"ok": True, "wav": payload}
        except Exception as error:  # noqa: BLE001 - report to the app, never crash the loop
            reply = {"ok": False, "error": "%s: %s" % (type(error).__name__, error)}

        sys.stdout.write(json.dumps(reply) + "\n")
        sys.stdout.flush()


if __name__ == "__main__":
    main()
