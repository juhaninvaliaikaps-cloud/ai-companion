Whisper weights are not in git. They are large, and this project keeps audio on the PC.

In the Unity Editor choose AI Companion > Download Whisper Model (base).
That saves Assets/StreamingAssets/Whisper/ggml-base.bin (multilingual, about 148 MB).
The same menu can fetch ggml-small.bin when you want higher quality.

A manual download of the same file:

  curl -L -o Assets/StreamingAssets/Whisper/ggml-base.bin https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin

Use the multilingual file, not ggml-base.en.bin. Finnish and English both need the multilingual weight.
The .bin file is gitignored. Setup and Play mode steps are in docs/VOICE.md.
