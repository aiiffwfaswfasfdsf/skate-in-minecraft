"""Generates the board sounds in sounds/ (rolling, grinding, pop, land, flip).

Everything is synthesized from noise and sine waves, so the sounds are ours to share (MIT, like the code).
Run: python tools/make_sounds.py   (needs numpy)
"""
import os
import wave

import numpy as np

RATE = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "sounds")
rng = np.random.default_rng(7)


def shaped_noise(seconds, gain_for_freq):
    """White noise filtered in the frequency domain. FFT filtering is circular, so the result loops seamlessly."""
    n = int(RATE * seconds)
    spec = np.fft.rfft(rng.standard_normal(n))
    freqs = np.fft.rfftfreq(n, 1 / RATE)
    return np.fft.irfft(spec * gain_for_freq(freqs), n)


def band(freqs, center, width):
    return np.exp(-0.5 * ((freqs - center) / width) ** 2)


def decay(seconds, tau):
    t = np.arange(int(RATE * seconds)) / RATE
    return t, np.exp(-t / tau)


def save(name, x, peak=0.8):
    x = x / np.max(np.abs(x)) * peak
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((x * 32767).astype("<i2").tobytes())


# Rolling on asphalt: low rumble plus a little grit. Loops.
def roll_gain(f):
    return 1 / (1 + (f / 180) ** 2) + 0.12 * band(f, 1800, 900)


roll = shaped_noise(2.0, roll_gain)
t = np.arange(len(roll)) / RATE
roll *= 1 + 0.15 * np.sin(2 * np.pi * 3 * t)  # 3 Hz wheel wobble, whole periods so it still loops
save("roll", roll, 0.6)

# Grinding metal: noise through a few inharmonic resonances. Loops.
def grind_gain(f):
    return sum(band(f, c, w) for c, w in ((1180, 60), (2630, 90), (3910, 120), (5270, 160))) + 0.25 * band(f, 3000, 1500)


grind = shaped_noise(1.5, grind_gain)
t = np.arange(len(grind)) / RATE
grind *= 1 + 0.3 * np.sin(2 * np.pi * 4 * t) * np.sin(2 * np.pi * 2 / 1.5 * t)
save("grind", grind, 0.5)

# Pop: tail hitting the ground, a sharp click and a wooden "tock".
t, env = decay(0.35, 0.04)
click = rng.standard_normal(len(t)) * np.exp(-t / 0.003)
tock = np.sin(2 * np.pi * 150 * t) * env + 0.6 * np.sin(2 * np.pi * 650 * t) * np.exp(-t / 0.025)
save("pop", click * 0.8 + tock)

# Land: low thud, wheels slapping down, board clack.
t, env = decay(0.5, 0.12)
thud = np.sin(2 * np.pi * 65 * t) * env
clack = 0.7 * np.sin(2 * np.pi * 400 * t) * np.exp(-t / 0.03) + 0.4 * np.sin(2 * np.pi * 1100 * t) * np.exp(-t / 0.015)
slap = rng.standard_normal(len(t)) * np.exp(-t / 0.02) * 0.5
save("land", thud + clack + slap)

# Flip: short whoosh of the board spinning.
n = int(RATE * 0.25)
whoosh = shaped_noise(0.25, lambda f: band(f, 1400, 700))
whoosh *= np.sin(np.linspace(0, np.pi, n)) ** 2
save("flip", whoosh, 0.5)

print("sounds written to", os.path.abspath(OUT))
