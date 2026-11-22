"""Kokoro TTS server for the AI Companion.

Loads the model once and answers one request per line on stdin with one JSON line on
stdout, so the app never pays the model load cost per sentence.

Request : {"text": "...", "speed": 1.0, "voice": "af_heart", "threads": 2}
Response: {"ok": true, "threads": 2, "wav": "<base64 WAV>"}  or  {"ok": false, "error": "..."}
Warm    : {"warm": true, "threads": 2}  ->  {"ok": true, "threads": 2}

JUH-168: "threads" is optional. When it is set (the app sends it while a game is in front),
the sentence runs on an onnxruntime session limited to that many threads, at below-normal
priority, so Kokoro doesn't put every core at full load on top of the game. Without it, the
default session uses every core at normal priority. The reply names the threads it used
(0 is every core), so the app can tell an older copy of this script, which ignores the field.
The first sentence on a new limited session once took 21.6 s, so the app sends a "warm" request
as soon as a game comes to the front: it builds the session and speaks one word into nothing.
"""
import argparse
import base64
import io
import json
import sys

import soundfile as sf
from kokoro_onnx import Kokoro

MAX_THREADS = 16


def set_priority(below_normal):
    """Below-normal priority while limited, normal otherwise. Windows only; never fatal."""
    if sys.platform != "win32":
        return
    try:
        import ctypes

        below_normal_class = 0x00004000
        normal_class = 0x00000020
        kernel32 = ctypes.windll.kernel32
        kernel32.SetPriorityClass(kernel32.GetCurrentProcess(), below_normal_class if below_normal else normal_class)
    except Exception:  # noqa: BLE001 - priority is a nicety, never a reason to stop speaking
        pass


def limited_kokoro(model, voices, threads):
    """A Kokoro on an onnxruntime session with `threads` threads and no busy-waiting."""
    import onnxruntime as rt

    options = rt.SessionOptions()
    options.intra_op_num_threads = threads
    options.inter_op_num_threads = 1
    options.add_session_config_entry("session.intra_op.allow_spinning", "0")
    options.add_session_config_entry("session.inter_op.allow_spinning", "0")
    session = rt.InferenceSession(model, sess_options=options, providers=["CPUExecutionProvider"])
    return Kokoro.from_session(session, voices)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True)
    parser.add_argument("--voices", required=True)
    parser.add_argument("--voice", default="af_heart")
    parser.add_argument("--lang", default="en-us")
    args = parser.parse_args()

    default = Kokoro(args.model, args.voices)
    # Only the latest limited session is kept, so at most two copies of the model are loaded.
    limited = {"threads": 0, "kokoro": None}
    below_normal = False
    sys.stderr.write("kokoro: ready\n")
    sys.stderr.flush()

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            request = json.loads(line)
            try:
                threads = int(request.get("threads") or 0)
            except (TypeError, ValueError):
                threads = 0
            threads = max(0, min(MAX_THREADS, threads))

            if threads > 0 and (limited["threads"] != threads or limited["kokoro"] is None):
                limited["kokoro"] = None
                limited["kokoro"] = limited_kokoro(args.model, args.voices, threads)
                limited["threads"] = threads

            if request.get("warm"):
                if threads > 0:
                    # The first run of a new session is the slow part; pay it now, not mid-reply.
                    limited["kokoro"].create("Hi.", voice=request.get("voice") or args.voice, speed=1.0, lang=args.lang)
                sys.stdout.write(json.dumps({"ok": True, "threads": threads}) + "\n")
                sys.stdout.flush()
                continue

            text = (request.get("text") or "").strip()
            if not text:
                raise ValueError("empty text")
            speed = float(request.get("speed") or 1.0)
            voice = request.get("voice") or args.voice
            kokoro = limited["kokoro"] if threads > 0 else default

            if below_normal != (threads > 0):
                below_normal = threads > 0
                set_priority(below_normal)

            samples, rate = kokoro.create(text, voice=voice, speed=speed, lang=args.lang)
            buffer = io.BytesIO()
            sf.write(buffer, samples, rate, format="WAV")
            payload = base64.b64encode(buffer.getvalue()).decode("ascii")
            reply = {"ok": True, "threads": threads, "wav": payload}
        except Exception as error:  # noqa: BLE001 - report to the app, never crash the loop
            reply = {"ok": False, "error": "%s: %s" % (type(error).__name__, error)}

        sys.stdout.write(json.dumps(reply) + "\n")
        sys.stdout.flush()


if __name__ == "__main__":
    main()
