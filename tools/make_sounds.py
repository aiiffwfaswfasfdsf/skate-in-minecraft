"""Generates the board sounds in sounds/ (rolling, grinding, powerslide, pop, land, flip, bail).

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
roll /= np.max(np.abs(roll))
# "tack-tack": the wheels rolling over a joint in the sidewalk, front then back truck
for start in (0.31, 1.27):
    for gap in (0.0, 0.11):
        i = int((start + gap) * RATE)
        tt, env = decay(0.06, 0.012)
        roll[i:i + len(tt)] += 0.35 * (np.sin(2 * np.pi * 220 * tt) + 0.5 * rng.standard_normal(len(tt))) * env
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
scuff = shaped_noise(0.35, lambda f: band(f, 2500, 1200)) * np.exp(-t / 0.06) * 0.25  # griptape scuff
save("pop", click * 0.8 + tock + scuff)

# Land: low thud, wheels slapping down, board clack.
t, env = decay(0.5, 0.12)
thud = np.sin(2 * np.pi * 65 * t) * env
clack = 0.7 * np.sin(2 * np.pi * 400 * t) * np.exp(-t / 0.03) + 0.4 * np.sin(2 * np.pi * 1100 * t) * np.exp(-t / 0.015)
slap = rng.standard_normal(len(t)) * np.exp(-t / 0.02) * 0.5
rattle = np.zeros(len(t))
for k, at in enumerate((0.04, 0.07, 0.095)):  # trucks rattling after the impact
    i = int(at * RATE)
    tt, env2 = decay(0.04, 0.008)
    rattle[i:i + len(tt)] += (0.35 / (k + 1)) * np.sin(2 * np.pi * 1800 * tt) * env2
save("land", thud + clack + slap + rattle)

# Flip: short whoosh of the board spinning.
n = int(RATE * 0.25)
whoosh = shaped_noise(0.25, lambda f: band(f, 1400, 700))
whoosh *= np.sin(np.linspace(0, np.pi, n)) ** 2
save("flip", whoosh, 0.5)

# Powerslide: urethane wheels scraping sideways over asphalt. Loops.
slide = shaped_noise(1.0, lambda f: band(f, 900, 450) + 0.5 * band(f, 2200, 500) + 0.3 / (1 + (f / 300) ** 2))
t = np.arange(len(slide)) / RATE
slide *= 1 + 0.25 * np.sin(2 * np.pi * 7 * t)  # 7 Hz chatter
save("slide", slide, 0.5)

# Bail: the board tumbling away, a few clacks that get quieter.
t = np.arange(int(RATE * 1.0)) / RATE
bail = np.zeros(len(t))
for k, at in enumerate((0.0, 0.18, 0.33, 0.47, 0.58, 0.67)):
    i = int(at * RATE)
    tt, env = decay(0.15, 0.03)
    f = 300 + 120 * rng.random()
    hit = np.sin(2 * np.pi * f * tt) + 0.5 * np.sin(2 * np.pi * f * 3.1 * tt) + 0.4 * rng.standard_normal(len(tt))
    bail[i:i + len(tt)] += hit * env * (0.85 ** k)
save("bail", bail, 0.8)

print("sounds written to", os.path.abspath(OUT))
