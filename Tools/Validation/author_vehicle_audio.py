"""Prepare a recorded engine loop and a soft original two-tone police siren.

First argument: downloaded loop_0.wav by domasx2 (CC0), from
https://opengameart.org/content/racing-car-engine-sound-loops
Run from the project root. Requires numpy; no runtime audio generation.
"""
from pathlib import Path
import sys
import wave
import numpy as np


def save(path, samples, rate):
    assert np.isfinite(samples).all() and np.max(np.abs(samples)) < 1
    with wave.open(str(path), "wb") as output:
        output.setparams((1, 2, rate, len(samples), "NONE", "not compressed"))
        output.writeframes((samples * 32767).astype("<i2").tobytes())


source = Path(sys.argv[1] if len(sys.argv) > 1 else "Temp/SoundDesign/engine-original.wav")
folder = Path("Assets/SoundEffect")
with wave.open(str(source), "rb") as stream:
    assert stream.getsampwidth() == 2
    rate = stream.getframerate()
    engine = np.frombuffer(stream.readframes(stream.getnframes()), dtype="<i2").astype(float)
    engine = engine.reshape(-1, stream.getnchannels()).mean(axis=1) / 32768

engine -= engine.mean()
overlap = min(int(rate * .09), len(engine) // 4)
mix = .5 - .5 * np.cos(np.linspace(0, np.pi, overlap))
engine[:overlap] = engine[-overlap:] * (1 - mix) + engine[:overlap] * mix
engine = engine[:-overlap]
# Periodic EQ preserves the seamless join while softening the harsh high end.
frequencies = np.fft.rfftfreq(len(engine), 1 / rate)
eq = (1 - np.exp(-(frequencies / 45) ** 2)) / np.sqrt(1 + (frequencies / 4000) ** 6)
engine = np.fft.irfft(np.fft.rfft(engine) * eq, n=len(engine))
engine *= .75 / np.max(np.abs(engine))
save(folder / "VehicleEngineLoop.wav", engine, rate)

rate = 44100
time = np.arange(round(rate * 5.6)) / rate
frequency = 577.5 + 82.5 * np.tanh(9 * np.sin(2 * np.pi * time / 1.4))
frequency += 1.8 * np.sin(2 * np.pi * 5 * time)
phase = 2 * np.pi * np.cumsum(frequency) / rate
siren = .7 * np.sin(phase) + .12 * np.sin(2 * phase) + .025 * np.sin(3 * phase)
siren -= siren.mean()
siren *= .62 / np.max(np.abs(siren))
save(folder / "PoliceTwoToneLoop.wav", siren, rate)
for name, values in [("engine", engine), ("siren", siren)]:
    print(name, "peak", round(float(np.max(np.abs(values))), 4), "rms", round(float(np.sqrt(np.mean(values ** 2))), 4), "loop delta", round(float(abs(values[-1] - values[0])), 4))
