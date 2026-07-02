import os
import sys
import subprocess

os.environ.setdefault("PYTHONIOENCODING", "utf-8")

# Ensure NLTK is installed
try:
    import nltk
except ImportError:
    subprocess.check_call([sys.executable, '-m', 'pip', 'install', 'nltk'])
    import nltk

# Ensure the necessary NLTK resources are available
for _resource in ("punkt_tab", "punkt"):
    try:
        nltk.data.find(f"tokenizers/{_resource}")
    except LookupError:
        nltk.download(_resource)

from vgpt import tts

# Read configuration variables
with open("Assets/VoiceGPT/Models/config.txt", "r") as file:
    for line in file:
        variable, value = line.split("=")
        variable = variable.strip()
        value = value.strip()
        exec(variable + " = " + value)

# Initialize TTS
_tts = tts.VGPT(Model, Config, ASRModel, ASRConfig, F0Model, BERTModel, BERTConfig)

# Run inference with or without embedding scaling
if _enableEScale:
    _tts.inference(
        _text,
        _targetVoice,
        _outputPath,
        embedding_scale=_eScale,
        alpha=_alpha,
        beta=_beta,
        diffusion_steps=_steps
    )
else:
    _tts.inference(
        _text,
        _targetVoice,
        _outputPath,
        alpha=_alpha,
        beta=_beta,
        diffusion_steps=_steps
    )
