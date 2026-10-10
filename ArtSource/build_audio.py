# Makes all of dreamWorld's music and sound effects from scratch (no samples, nothing downloaded).
#   python ArtSource/build_audio.py   ->  Assets/Resources/Audio/Music/*.wav and Assets/Resources/Audio/Sfx/*.wav
# Everything is 22.05 kHz mono 16-bit (a PS1-ish sample rate). Music loops are rendered circularly
# (notes that ring past the end wrap around to the start, reverb is a circular convolution), so they loop
# with no click. Levels: music is normalized to about -18 dBFS RMS, sound effects peak under -3 dBFS.
# The random seed is fixed so re-running gives the same files.
import os
import wave

import numpy as np
from scipy import signal

SR = 22050
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Audio")
MUSIC = os.path.join(ROOT, "Music")
SFX = os.path.join(ROOT, "Sfx")
rng = np.random.default_rng(7)


# ---------------------------------------------------------------- basics

def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)


NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6,
        "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def n(name):
    """'A4' -> midi number."""
    pitch, octave = name[:-1], int(name[-1])
    return 12 * (octave + 1) + NOTE[pitch]


def t(seconds):
    return np.arange(int(seconds * SR)) / SR


def env_adsr(length, a=0.01, d=0.1, s=0.7, r=0.2):
    total = len(length) if hasattr(length, "__len__") else int(length)
    e = np.ones(total) * s
    na, nd, nr = int(a * SR), int(d * SR), int(r * SR)
    na = min(na, total)
    e[:na] = np.linspace(0, 1, na, endpoint=False)
    nd = min(nd, total - na)
    e[na:na + nd] = np.linspace(1, s, nd, endpoint=False)
    nr = min(nr, total)
    if nr > 0:
        e[-nr:] *= np.linspace(1, 0, nr)
    return e


def lowpass(x, hz, order=2):
    b, a = signal.butter(order, min(hz, SR * 0.45) / (SR / 2), "low")
    return signal.lfilter(b, a, x)


def highpass(x, hz, order=2):
    b, a = signal.butter(order, hz / (SR / 2), "high")
    return signal.lfilter(b, a, x)


def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), min(hi, SR * 0.45) / (SR / 2)], "band")
    return signal.lfilter(b, a, x)


def sweep_filter(x, f0, f1, q=2.0, mode="band"):
    """State-variable filter whose cutoff glides from f0 to f1 (exponentially) over the sound."""
    out = np.zeros_like(x)
    low = band = 0.0
    freqs = f0 * (f1 / f0) ** np.linspace(0, 1, len(x))
    damp = 1.0 / q
    for i, s in enumerate(x):
        f = 2 * np.sin(np.pi * min(freqs[i], SR * 0.2) / SR)
        high = s - low - damp * band
        band += f * high
        low += f * band
        out[i] = band if mode == "band" else low
    return out


def noise(seconds):
    return rng.uniform(-1, 1, int(seconds * SR))


def osc(freq, tt, kind="sine", harmonics=10):
    """Band-limited oscillators (additive, so nothing aliases at 22 kHz)."""
    if kind == "sine":
        return np.sin(2 * np.pi * freq * tt)
    out = np.zeros_like(tt)
    for k in range(1, harmonics + 1):
        if freq * k > SR * 0.45:
            break
        if kind == "saw":
            out += np.sin(2 * np.pi * freq * k * tt) / k
        elif kind == "square" and k % 2:
            out += np.sin(2 * np.pi * freq * k * tt) / k
        elif kind == "tri" and k % 2:
            out += ((-1) ** ((k - 1) // 2)) * np.sin(2 * np.pi * freq * k * tt) / (k * k)
    return out


# ---------------------------------------------------------------- instruments (each returns a mono note)

def music_box(freq, dur=2.2):
    tt = t(dur)
    e = np.exp(-tt * 3.2)
    x = (np.sin(2 * np.pi * freq * tt) + 0.35 * np.sin(2 * np.pi * freq * 4.0 * tt) * np.exp(-tt * 9)
         + 0.18 * np.sin(2 * np.pi * freq * 6.9 * tt) * np.exp(-tt * 14))
    x[:40] *= np.linspace(0, 1, 40)
    return x * e


def bell(freq, dur=3.0):
    tt = t(dur)
    parts = [(1.0, 1.0, 2.0), (2.76, 0.4, 4.0), (5.4, 0.2, 7.0), (0.5, 0.25, 1.4)]
    x = sum(a * np.sin(2 * np.pi * freq * r * tt) * np.exp(-tt * d) for r, a, d in parts)
    x[:30] *= np.linspace(0, 1, 30)
    return x


def pluck(freq, dur=1.2, bright=0.5):
    """Karplus-Strong string: a kalimba/harp-ish pluck."""
    period = max(2, int(round(SR / freq)))
    buf = rng.uniform(-1, 1, period)
    buf = lowpass(buf, 1500 + 6000 * bright, 1)
    total = int(dur * SR)
    out = np.zeros(total)
    for i in range(total):
        j = i % period
        out[i] = buf[j]
        buf[j] = 0.5 * (buf[j] + buf[(j + 1) % period]) * 0.996
    return out * env_adsr(total, 0.002, 0.0, 1.0, 0.1)


def piano(freq, dur=4.0, vel=1.0):
    tt = t(dur)
    x = np.zeros_like(tt)
    for k in range(1, 8):
        f = freq * k * (1 + 0.0004 * k * k)   # a little stretched, like real strings
        if f > SR * 0.45:
            break
        x += (0.8 ** k) * np.sin(2 * np.pi * f * tt) * np.exp(-tt * (0.9 + 0.7 * k))
    x += 0.04 * lowpass(noise(dur), 2500) * np.exp(-tt * 60)   # hammer
    x[:20] *= np.linspace(0, 1, 20)
    return x * vel


def pad(freqs, dur, attack=1.2, release=1.4, bright=1400, kind="saw"):
    tt = t(dur)
    x = np.zeros_like(tt)
    for f in freqs:
        for det in (-0.12, 0.0, 0.13):   # three slightly detuned voices: chorus warmth
            ff = f * 2 ** (det / 12)
            x += osc(ff, tt + rng.uniform(0, 1), kind, 8)
    x = lowpass(x, bright, 2)
    lfo = 1 + 0.08 * np.sin(2 * np.pi * 0.23 * tt)
    return x * env_adsr(len(tt), attack, 0.0, 1.0, release) * lfo / (len(freqs) * 3)


def sub_bass(freq, dur):
    tt = t(dur)
    x = np.sin(2 * np.pi * freq * tt) + 0.25 * osc(freq, tt, "tri", 5)
    return x * env_adsr(len(tt), 0.02, 0.3, 0.6, 0.25)


def kick(soft=1.0):
    tt = t(0.35)
    f = 40 + 90 * np.exp(-tt * 30)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 12)
    return x * soft


def shaker(dur=0.08):
    x = highpass(noise(dur), 5000)
    return x * np.exp(-t(dur) * 50)


def rim(dur=0.12):
    tt = t(dur)
    x = 0.6 * np.sin(2 * np.pi * 820 * tt) * np.exp(-tt * 50) + 0.5 * bandpass(noise(dur), 1500, 5000) * np.exp(-tt * 80)
    return x


def taiko(dur=0.6):
    tt = t(dur)
    f = 55 + 70 * np.exp(-tt * 18)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 6)
    x += 0.3 * lowpass(noise(dur), 900) * np.exp(-tt * 25)
    return x


# ---------------------------------------------------------------- loop helpers

class Loop:
    """A circular buffer: anything that rings past the end of the loop wraps to the start."""

    def __init__(self, seconds):
        self.n = int(round(seconds * SR))
        self.buf = np.zeros(self.n)

    def add(self, sound, at, gain=1.0):
        start = int(round(at * SR)) % self.n
        idx = (start + np.arange(len(sound))) % self.n
        np.add.at(self.buf, idx, sound * gain)

    def bus(self):
        return Loop._from(self.n)

    @staticmethod
    def _from(count):
        l = Loop(1)
        l.n = count
        l.buf = np.zeros(count)
        return l


def circular_reverb(x, seconds=2.2, damp=4000, mix=0.3):
    """Convolves with a decaying noise tail, circularly (FFT), so the loop stays seamless."""
    ir_len = int(seconds * SR)
    ir = rng.standard_normal(ir_len) * np.exp(-np.arange(ir_len) / SR * (6.9 / seconds))
    ir = lowpass(ir, damp)
    ir[: int(0.012 * SR)] = 0   # a short pre-delay
    ir /= np.sqrt(np.sum(ir ** 2))
    padded = np.zeros(len(x))
    padded[: min(ir_len, len(x))] = ir[: len(x)]
    wet = np.real(np.fft.ifft(np.fft.fft(x) * np.fft.fft(padded)))
    return x * (1 - mix) + wet * mix * 1.6


def circular(fn, x):
    """Runs a (stateful) filter over three copies of a loop and keeps the middle one: seamless."""
    y = fn(np.tile(x, 3))
    return y[len(x): 2 * len(x)]


def loop_noise(seconds, shape):
    """Stationary colored noise that loops perfectly: shaped in the frequency domain."""
    count = int(round(seconds * SR))
    spec = np.fft.rfft(rng.standard_normal(count))
    freqs = np.fft.rfftfreq(count, 1 / SR)
    x = np.fft.irfft(spec * shape(freqs), count)
    return x / (np.std(x) + 1e-9)


def periodic_lfo(count, cycles, phase=0.0):
    return np.sin(2 * np.pi * (cycles * np.arange(count) / count + phase))


# ---------------------------------------------------------------- output

LEVELS = []


def db(v):
    return 20 * np.log10(max(v, 1e-9))


def write(path, x, rms_db=None, peak_db=None):
    x = np.asarray(x, dtype=np.float64)
    x = x - np.mean(x)
    if rms_db is not None:
        x *= 10 ** (rms_db / 20) / (np.sqrt(np.mean(x ** 2)) + 1e-12)
        pk = np.max(np.abs(x))
        if pk > 0.89:   # soft-limit the odd peak instead of clipping
            x = np.tanh(x / 0.89) * 0.89
    if peak_db is not None:
        x *= 10 ** (peak_db / 20) / (np.max(np.abs(x)) + 1e-12)
    else:
        if rms_db is None:
            raise ValueError("need a level")
    pcm = np.clip(np.round(x * 32767), -32768, 32767).astype(np.int16)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    y = pcm / 32768.0
    LEVELS.append((os.path.relpath(path, ROOT).replace("\\", "/"), len(y) / SR,
                   db(np.sqrt(np.mean(y ** 2))), db(np.max(np.abs(y)))))


def fade_edges(x, ms_in=3, ms_out=20):
    a, b = int(ms_in * SR / 1000), int(ms_out * SR / 1000)
    x = x.copy()
    if a:
        x[:a] *= np.linspace(0, 1, a)
    if b:
        x[-b:] *= np.linspace(1, 0, b)
    return x


# ================================================================ music

def chord_freqs(names):
    return [midi(n(c)) for c in names]


def town():
    """Mushroom Hollow town square at night: F major, 80 bpm, music box over warm pads. 16 bars = 48 s."""
    bpm, bars = 80, 16
    beat = 60 / bpm
    bar = beat * 4
    L = Loop(bar * bars)
    pads, keys, bass, drums = L.bus(), L.bus(), L.bus(), L.bus()
    prog = [("F3", "A3", "C4", "E4"), ("D3", "F3", "A3", "C4"), ("Bb2", "D3", "F3", "A3"), ("C3", "E3", "G3", "Bb3"),
            ("F3", "A3", "C4", "E4"), ("A2", "C3", "E3", "G3"), ("Bb2", "D3", "F3", "A3"), ("C3", "E3", "G3", "C4")]
    roots = ["F2", "D2", "Bb1", "C2", "F2", "A1", "Bb1", "C2"]
    for i in range(bars):
        c = i % 8
        pads.add(pad(chord_freqs(prog[c]), bar + 1.6, 0.9, 1.6, 1100), i * bar)
        bass.add(sub_bass(midi(n(roots[c])), beat * 2.2), i * bar, 0.9)
        bass.add(sub_bass(midi(n(roots[c])) * (1.5 if c % 2 else 1.0), beat * 1.6), i * bar + beat * 2.5, 0.6)
    # the melody: (beat offset within the 8-bar phrase, note, length in beats)
    phrase_a = [(0, "C5", 1), (1, "F5", 1), (2, "A5", 1.5), (3.5, "G5", .5), (4, "F5", 1), (5, "D5", 1), (6, "A5", 2),
                (8, "Bb5", 1), (9, "A5", .5), (9.5, "G5", .5), (10, "F5", 1), (11, "D5", 1), (12, "C5", 1), (13, "E5", 1), (14, "G5", 2),
                (16, "A5", 1), (17, "C6", 1), (18, "A5", 1), (19, "F5", 1), (20, "E5", 1.5), (21.5, "F5", .5), (22, "G5", 2),
                (24, "F5", 1), (25, "D5", 1), (26, "Bb4", 1), (27, "D5", 1), (28, "C5", 1), (29, "E5", .5), (29.5, "G5", .5), (30, "F5", 2)]
    phrase_b = [(0, "A5", 1.5), (1.5, "G5", .5), (2, "F5", 1), (3, "C5", 1), (4, "D5", 1), (5, "F5", 1), (6, "E5", 2),
                (8, "D5", 1), (9, "F5", 1), (10, "Bb5", 1), (11, "A5", 1), (12, "G5", 2), (14, "C5", 2),
                (16, "F5", .5), (16.5, "G5", .5), (17, "A5", 1), (18, "C6", 2), (20, "Bb5", 1), (21, "A5", 1), (22, "G5", 2),
                (24, "A5", 1), (25, "G5", 1), (26, "F5", 1), (27, "D5", 1), (28, "E5", 2), (30, "F5", 2)]
    for half, phrase in enumerate((phrase_a, phrase_b)):
        for b0, note, length in phrase:
            at = half * 8 * bar + b0 * beat
            keys.add(music_box(midi(n(note))), at, 0.55)
            if length >= 2:   # long notes get a soft bell an octave down
                keys.add(bell(midi(n(note)) / 2, 2.5), at, 0.12)
    # soft lo-fi drums: kick on 1 and the "and" of 3, rim on 2 and 4, a quiet shaker
    for i in range(bars):
        for b in range(4):
            at = i * bar + b * beat
            if b == 0:
                drums.add(kick(), at, 0.55)
            if b == 2:
                drums.add(kick(), at + beat * 0.5, 0.35)
            if b in (1, 3):
                drums.add(rim(), at, 0.12)
            drums.add(shaker(), at, 0.05)
            drums.add(shaker(), at + beat * 0.5 + 0.03, 0.08)   # a touch of swing
    mix = pads.buf * 0.9 + keys.buf * 0.85 + bass.buf * 0.45 + drums.buf
    mix = circular(lambda x: lowpass(x, 5200, 2), mix)   # lo-fi top end
    mix = circular_reverb(mix, 2.4, 3800, 0.28)
    vinyl = loop_noise(L.n / SR, lambda f: np.where(f > 0, 1 / np.sqrt(np.maximum(f, 50)), 0) * (f < 6000)) * 0.004
    return mix + vinyl


def woods():
    """The wilds around town at night: A dorian, sparse kalimba plucks, airy pad, crickets. 14 bars at 70 bpm = 48 s."""
    bpm, bars = 70, 14
    beat = 60 / bpm
    bar = beat * 4
    L = Loop(bar * bars)
    pads, plucks, amb = L.bus(), L.bus(), L.bus()
    prog = [("A2", "E3", "B3", "C4"), ("A2", "E3", "G3", "D4"), ("F2", "C3", "G3", "A3"), ("G2", "D3", "A3", "B3"),
            ("A2", "E3", "B3", "C4"), ("D3", "A3", "C4", "F#4"), ("E2", "B2", "G3", "A3")]
    for i in range(0, bars, 2):
        c = (i // 2) % len(prog)
        pads.add(pad(chord_freqs(prog[c]), bar * 2 + 2.5, 2.5, 2.5, 800, "tri"), i * bar)
    scale = ["A4", "B4", "C5", "D5", "E5", "F#5", "G5", "A5", "E4"]
    motif = [(0, 4), (1.5, 2), (2, 1), (6, 3), (7.5, 4), (10, 0), (12, 2), (13, 3), (14.5, 4)]   # (beat, scale index)
    for k in range(bars // 4 + 1):
        shift = [0, 1, -1, 2][k % 4]
        for b0, idx in motif:
            at = k * 4 * bar + b0 * beat
            if at >= L.n / SR:
                continue
            j = max(0, min(len(scale) - 1, idx + shift))
            plucks.add(pluck(midi(n(scale[j])), 1.6, 0.35), at, 0.45)
            if rng.random() < 0.3:   # an echo, an octave up and quiet
                plucks.add(pluck(midi(n(scale[j])) * 2, 1.0, 0.2), at + beat * 0.75, 0.12)
    # a low, slow owl-ish hoot twice per loop
    for at in (bar * 3 + beat, bar * 10 + beat * 2):
        tt = t(0.9)
        hoot = np.sin(2 * np.pi * (390 - 40 * tt) * tt) * env_adsr(len(tt), 0.08, 0.1, 0.7, 0.3)
        amb.add(np.concatenate([hoot, np.zeros(int(0.25 * SR)), hoot * 0.8]), at, 0.07)
    # crickets: bursts of a ~4.6 kHz chirp, each cricket on its own rhythm (whole number per loop, so it loops)
    total = L.n / SR
    for c in range(5):
        f = 4300 + 180 * c
        period = total / int(total / (0.55 + 0.17 * c))
        tt = t(0.12)
        trill = np.sin(2 * np.pi * f * tt) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 70 * tt))) * env_adsr(len(tt), 0.005, 0.0, 1.0, 0.03)
        k = 0
        while k * period < total:
            amb.add(trill, k * period + c * 0.11, 0.035 * (0.6 + 0.4 * rng.random()))
            k += 1
    wind = loop_noise(total, lambda f: np.exp(-((f - 350) / 300) ** 2)) * (0.7 + 0.3 * periodic_lfo(L.n, 3))
    mix = pads.buf * 1.1 + plucks.buf + amb.buf + wind * 0.015
    mix = circular_reverb(mix, 3.2, 3000, 0.4)
    return mix


def mistpine():
    """Mistpine Hollow: foggy and moody. D minor, 60 bpm, slow piano over a low drone and rain. 12 bars = 48 s."""
    bpm, bars = 60, 12
    beat = 60 / bpm
    bar = beat * 4
    L = Loop(bar * bars)
    keys, pads, rain = L.bus(), L.bus(), L.bus()
    prog = [("D3", "F3", "A3"), ("Bb2", "D3", "F3"), ("G2", "Bb2", "D3"), ("A2", "C#3", "E3"),
            ("D3", "F3", "A3"), ("F2", "A2", "C3"), ("G2", "Bb2", "D3"), ("A2", "C#3", "E3"),
            ("Bb2", "D3", "F3"), ("G2", "Bb2", "D3"), ("D3", "F3", "A3"), ("A2", "C#3", "G3")]
    for i in range(bars):
        ch = prog[i]
        pads.add(pad(chord_freqs(ch), bar + 2.0, 1.8, 2.0, 600, "saw"), i * bar)
        keys.add(piano(midi(n(ch[0])) / 2, 5.0, 0.5), i * bar)   # low octave on the downbeat
        # a slow broken chord, upper voice wandering
        arp = [ch[0], ch[1], ch[2]]
        for b, name in enumerate(arp):
            keys.add(piano(midi(n(name)) * 2, 3.5, 0.45), i * bar + beat * (b + 0.5))
    melody = [(0, "A5"), (6, "F5"), (8, "D5"), (12, "E5"), (14, "C#5"), (16, "D5"), (20, "A5"), (22, "G5"), (24, "F5"),
              (28, "E5"), (32, "D5"), (35, "F5"), (36, "Bb5"), (40, "A5"), (42, "F5"), (44, "E5"), (46, "C#5")]
    for b0, name in melody:
        keys.add(piano(midi(n(name)), 4.0, 0.55), b0 * beat)
    total = L.n / SR
    hiss = loop_noise(total, lambda f: np.where(f > 0, 1.0, 0) * np.clip((f - 600) / 2500, 0, 1) * np.exp(-f / 7000))
    rumble = loop_noise(total, lambda f: np.exp(-((f - 120) / 90) ** 2))
    swell = 0.75 + 0.25 * periodic_lfo(L.n, 2, 0.1)
    for _ in range(int(total * 14)):   # individual drops pattering on leaves
        tt = t(0.03)
        drop = np.sin(2 * np.pi * rng.uniform(1800, 3800) * tt) * np.exp(-tt * 180)
        rain.add(drop, rng.uniform(0, total), rng.uniform(0.01, 0.04))
    drone = pad([midi(n("D2")), midi(n("A2"))], total + 4, 3, 3, 300, "saw")
    keys_wet = circular_reverb(keys.buf, 3.5, 2500, 0.45)
    mix = keys_wet * 0.8 + circular_reverb(pads.buf, 3.0, 1500, 0.4) * 0.9
    mix = mix + rain.buf + hiss * swell * 0.05 + rumble * 0.03
    loopdrone = Loop(total)
    loopdrone.add(drone, 0)
    return mix + loopdrone.buf * 0.5


def combat():
    """A tense layer that fades in over the area music while something chases you. E minor, 120 bpm, 4 bars = 8 s."""
    bpm, bars = 120, 4
    beat = 60 / bpm
    bar = beat * 4
    L = Loop(bar * bars)
    roots = ["E2", "E2", "C2", "D2"]
    for i in range(bars):
        f = midi(n(roots[i]))
        for e in range(8):   # pulsing eighth-note bass
            tt = t(beat * 0.45)
            note = osc(f, tt, "saw", 12) * env_adsr(len(tt), 0.005, 0.08, 0.5, 0.05)
            L.add(lowpass(note, 900), i * bar + e * beat / 2, 0.5 if e % 2 else 0.7)
        L.add(taiko(), i * bar, 0.9)
        L.add(taiko(), i * bar + beat * 1.5, 0.5)
        L.add(taiko(), i * bar + beat * 2, 0.8)
        L.add(rim(), i * bar + beat * 3.5, 0.3)
        for s in range(16):
            L.add(shaker(0.05), i * bar + s * beat / 4, 0.06 if s % 2 else 0.1)
    # a high, nervous tremolo
    tt = t(L.n / SR)
    trem = osc(midi(n("B4")), tt, "saw", 6) + osc(midi(n("C5")), tt, "saw", 6) * 0.6
    trem *= 0.5 + 0.5 * np.sin(2 * np.pi * 8 * tt)
    mix = L.buf + lowpass(trem, 2500) * 0.05
    return circular_reverb(mix, 1.4, 4000, 0.18)


# ================================================================ sound effects

def step(stone, seed):
    r = np.random.default_rng(100 + seed + (50 if stone else 0))
    if stone:
        dur = 0.09
        tt = t(dur)
        x = bandpass(r.uniform(-1, 1, len(tt)), 1200 + 300 * seed, 5000) * np.exp(-tt * 70)
        x += 0.5 * np.sin(2 * np.pi * (180 + 20 * seed) * tt) * np.exp(-tt * 60)
    else:
        dur = 0.16
        tt = t(dur)
        x = bandpass(r.uniform(-1, 1, len(tt)), 400 + 100 * seed, 3500) * np.exp(-tt * 30)
        for _ in range(6):   # crunchy grass blades
            k = int(r.uniform(0, 0.08) * SR)
            x[k:k + 40] += r.uniform(-0.6, 0.6, len(x[k:k + 40]))
        x += 0.4 * np.sin(2 * np.pi * 90 * tt) * np.exp(-tt * 40)
        x = lowpass(x, 4000)
    return fade_edges(x, 1, 10)


def whoosh(dur, f0, f1, q=1.6):
    tt = t(dur)
    x = sweep_filter(noise(dur), f0, f1, q)
    e = np.sin(np.pi * np.clip(tt / dur, 0, 1)) ** 1.5
    return fade_edges(x * e, 1, 10)


def build_sfx():
    S = lambda name, x, pk=-4.0: write(os.path.join(SFX, name + ".wav"), x, peak_db=pk)
    for i in range(4):
        S(f"step_grass_{i + 1}", step(False, i), -9)
        S(f"step_stone_{i + 1}", step(True, i), -10)

    S("swing", whoosh(0.26, 600, 3200), -6)
    heavy = whoosh(0.45, 300, 1800, 1.3) + 0.3 * lowpass(whoosh(0.45, 200, 600, 1.0), 500)
    S("swing_heavy", heavy, -5)

    tt = t(0.25)
    thud = np.sin(2 * np.pi * np.cumsum(55 + 140 * np.exp(-tt * 25)) / SR) * np.exp(-tt * 14)
    thud += 0.6 * lowpass(noise(0.25), 2500) * np.exp(-tt * 45)
    S("hit", fade_edges(thud, 0, 20), -3.5)

    tt = t(0.32)
    f = 520 * np.exp(-tt * 2.2) * (1 + 0.03 * np.sin(2 * np.pi * 28 * tt))
    hurt = lowpass(np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)), 2200) * env_adsr(len(tt), 0.005, 0.05, 0.7, 0.12)
    hurt[: len(thud)] += 0.8 * thud[: len(hurt)]
    S("hurt", hurt, -4)

    # monster defeat: an airy poof and a little sparkle going up
    tt = t(0.7)
    poof = sweep_filter(noise(0.7), 2500, 250, 0.9, "low") * np.exp(-tt * 6)
    for k, note in enumerate(["E6", "G#6", "B6", "E7"]):
        sp = bell(midi(n(note)), 0.4) * 0.12
        start = int((0.08 + 0.05 * k) * SR)
        poof[start:start + len(sp)] += sp[: len(poof) - start]
    S("defeat", fade_edges(poof, 2, 40), -5)

    # coin: the classic two-note ding (square-ish, softened)
    tt1, tt2 = t(0.07), t(0.35)
    c1 = osc(midi(n("B5")), tt1, "square", 6) * 0.7
    c2 = osc(midi(n("E6")), tt2, "square", 6) * np.exp(-tt2 * 9)
    S("coin", fade_edges(lowpass(np.concatenate([c1, c2]), 6000), 1, 30), -6)

    # item pickup: a rising bell arpeggio
    item = np.zeros(int(0.65 * SR))
    for k, note in enumerate(["C5", "E5", "G5", "C6"]):
        b = bell(midi(n(note)), 0.5)
        s = int(k * 0.055 * SR)
        item[s:s + len(b)] += b[: len(item) - s]
    S("item", fade_edges(item, 1, 60), -6)

    tt = t(0.035)
    S("ui_click", fade_edges(np.sin(2 * np.pi * 1500 * tt) * np.exp(-tt * 120) + 0.3 * highpass(noise(0.035), 3000) * np.exp(-tt * 300), 0, 5), -10)
    tt = t(0.03)
    S("ui_hover", fade_edges(np.sin(2 * np.pi * 2400 * tt) * np.exp(-tt * 150), 1, 5), -18)

    def bag(up):
        tt = t(0.3)
        rustle = bandpass(noise(0.3), 1500, 6000) * (np.exp(-tt * 12) * (0.6 + 0.4 * np.sin(2 * np.pi * 30 * tt)))
        notes = ["G5", "C6"] if up else ["C6", "G5"]
        x = rustle * 0.4
        for k, note in enumerate(notes):
            b = music_box(midi(n(note)), 0.25) * 0.6
            s = int((0.03 + 0.08 * k) * SR)
            x[s:s + len(b)] += b[: len(x) - s]
        return fade_edges(x, 1, 30)
    S("bag_open", bag(True), -8)
    S("bag_close", bag(False), -9)

    # level up: a sparkly little fanfare
    lv = np.zeros(int(1.8 * SR))
    for k, (note, at) in enumerate([("C5", 0), ("E5", .1), ("G5", .2), ("C6", .3), ("E6", .45), ("G6", .45)]):
        b = bell(midi(n(note)), 1.3) * (0.8 if k < 4 else 0.5)
        s = int(at * SR)
        lv[s:s + len(b)] += b[: len(lv) - s]
    chord = pad(chord_freqs(("C4", "E4", "G4", "C5")), 1.4, 0.05, 0.8, 2500, "square")
    s = int(0.3 * SR)
    lv[s:s + len(chord)] += chord[: len(lv) - s] * 1.5
    lv += highpass(noise(1.8), 6000) * np.exp(-t(1.8) * 3) * 0.04
    S("levelup", fade_edges(lv, 1, 200), -4)

    tt = t(0.16)
    jump = np.sin(2 * np.pi * np.cumsum(260 + 500 * tt / 0.16) / SR) * env_adsr(len(tt), 0.005, 0.05, 0.6, 0.06)
    jump += 0.3 * whoosh(0.16, 800, 2500)
    S("jump", jump, -12)

    roll = whoosh(0.4, 400, 1200, 1.2) * 0.7
    tt = t(0.4)
    roll += bandpass(noise(0.4), 300, 2000) * np.exp(-((tt - 0.25) / 0.06) ** 2) * 0.5   # land on the ground
    S("roll", fade_edges(roll, 1, 20), -8)

    tt = t(0.06)
    blip = np.concatenate([osc(midi(n("A5")), tt, "square", 5), osc(midi(n("E6")), t(0.09), "square", 5) * np.exp(-t(0.09) * 20)])
    S("blip", fade_edges(lowpass(blip, 5000), 1, 15), -13)

    eat = np.zeros(int(0.55 * SR))
    for k in range(3):
        d = 0.09
        tt = t(d)
        c = bandpass(noise(d), 1500, 7000) * np.exp(-tt * 35)
        c *= (np.random.default_rng(k).uniform(0, 1, len(tt)) > 0.6)   # crackly
        c += 0.3 * np.sin(2 * np.pi * 140 * tt) * np.exp(-tt * 40)
        s = int((0.03 + 0.17 * k) * SR)
        eat[s:s + len(c)] += c * (1 - 0.15 * k)
    S("eat", fade_edges(lowpass(eat, 7000), 1, 20), -6)


def main():
    write(os.path.join(MUSIC, "town.wav"), town(), rms_db=-18)
    write(os.path.join(MUSIC, "woods.wav"), woods(), rms_db=-19)
    write(os.path.join(MUSIC, "mistpine.wav"), mistpine(), rms_db=-19)
    write(os.path.join(MUSIC, "combat.wav"), combat(), rms_db=-20)
    build_sfx()
    print(f"{'file':28s} {'sec':>6s} {'RMS dBFS':>9s} {'peak dBFS':>10s}")
    for name, sec, rms, pk in LEVELS:
        print(f"{name:28s} {sec:6.2f} {rms:9.1f} {pk:10.1f}")


if __name__ == "__main__":
    main()
