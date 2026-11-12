"""Cuts the boot intro's scooter engine clip out of the moped recording: the loudest 2.6 s window, preferring one that
contains a rise in level (a rev), faded at both ends, mono 44.1 kHz 16-bit, peak-normalised to -3 dBFS.

Run: python tools/intro-engine.py
Reads Assets/Audio/TEST/78651__justinbw__moped-motor.wav, writes Assets/Audio/SFX/IntroEngine.wav and prints the
chosen start time, duration and peak. Uses only the wave module and numpy.
"""
import os
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(ROOT, "Assets", "Audio", "TEST", "78651__justinbw__moped-motor.wav")
DST = os.path.join(ROOT, "Assets", "Audio", "SFX", "IntroEngine.wav")

RATE = 44100
LENGTH = 2.6        # seconds
FADE_IN = 0.15
FADE_OUT = 0.6
PEAK_DB = -3.0
REV_RISE = 1.5      # last quarter of the window must be at least this much louder (RMS ratio) than the first quarter
STEP = 0.05         # window search step, seconds


def read(path):
    """Returns mono float samples in -1..1 and the sample rate, for 8, 16, 24 or 32 bit PCM, any channel count"""
    with wave.open(path, "rb") as w:
        ch, width, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(n)
    if width == 1:
        x = (np.frombuffer(raw, np.uint8).astype(np.float64) - 128) / 128
    elif width == 2:
        x = np.frombuffer(raw, "<i2").astype(np.float64) / 32768
    elif width == 3:
        b = np.frombuffer(raw, np.uint8).reshape(-1, 3).astype(np.int32)
        v = b[:, 0] | (b[:, 1] << 8) | (b[:, 2] << 16)
        v = np.where(v >= 1 << 23, v - (1 << 24), v)
        x = v.astype(np.float64) / (1 << 23)
    elif width == 4:
        x = np.frombuffer(raw, "<i4").astype(np.float64) / (1 << 31)
    else:
        raise ValueError("unsupported sample width %d" % width)
    return x.reshape(-1, ch).mean(axis=1), rate


def resample(x, rate):
    if rate == RATE:
        return x
    n = int(round(len(x) * RATE / rate))
    return np.interp(np.arange(n) * rate / RATE, np.arange(len(x)), x)


def pick(x):
    """Start sample of the loudest window, among windows with a rev if any have one"""
    n = int(LENGTH * RATE)
    step = int(STEP * RATE)
    csum = np.concatenate(([0.0], np.cumsum(x * x)))
    starts = np.arange(0, len(x) - n + 1, step)
    energy = csum[starts + n] - csum[starts]
    q = n // 4
    first = csum[starts + q] - csum[starts]
    last = csum[starts + n] - csum[starts + n - q]
    rising = last > first * REV_RISE ** 2
    pool = np.where(rising)[0] if rising.any() else np.arange(len(starts))
    return int(starts[pool[np.argmax(energy[pool])]]), bool(rising.any())


def main():
    x, rate = read(SRC)
    x = resample(x, rate)
    if len(x) < int(LENGTH * RATE):
        raise SystemExit("source is shorter than %.1f s" % LENGTH)
    start, rev = pick(x)
    clip = x[start:start + int(LENGTH * RATE)].copy()
    clip -= clip.mean()
    fi, fo = int(FADE_IN * RATE), int(FADE_OUT * RATE)
    clip[:fi] *= np.linspace(0, 1, fi)
    clip[-fo:] *= np.linspace(1, 0, fo)
    clip *= 10 ** (PEAK_DB / 20) / np.abs(clip).max()
    pcm = np.round(clip * 32767).astype("<i2")
    os.makedirs(os.path.dirname(DST), exist_ok=True)
    with wave.open(DST, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())
    peak = np.abs(pcm).max() / 32767
    print("start %.2f s, duration %.2f s, rev detected: %s, peak %.2f dBFS (%d/32767)"
          % (start / RATE, len(pcm) / RATE, rev, 20 * np.log10(peak), np.abs(pcm).max()))


if __name__ == "__main__":
    main()
