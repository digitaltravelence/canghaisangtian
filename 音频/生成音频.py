"""Rebuild the original score and foley with Python 3 and NumPy, beside this file."""
from pathlib import Path
import wave

import numpy as np


RATE = 32000
BEAT = 0.6
BAR = BEAT * 3
OUT = Path(__file__).resolve().parent
TAU = 2 * np.pi
random = np.random.default_rng(24731)


def timeline(seconds):
    return np.arange(round(seconds * RATE)) / RATE


def noise(t, center, width):
    """Band-limited, non-repeating foley grain."""
    frequencies = np.fft.rfftfreq(len(t), 1 / RATE)
    spectrum = np.fft.rfft(random.standard_normal(len(t)))
    spectrum *= np.exp(-0.5 * ((frequencies - center) / width) ** 2)
    signal = np.fft.irfft(spectrum, n=len(t))
    return signal / (np.sqrt(np.mean(signal * signal)) + 1e-8)


def release(t, seconds):
    return np.clip((t[-1] - t) / seconds, 0, 1) ** 2


def add(track, mono, start, level, pan=0, loop=False):
    angle = np.pi * (pan + 1) / 4
    stereo = mono[:, None] * np.array([np.cos(angle), np.sin(angle)]) * level
    first = round(start * RATE)
    if loop:
        first %= len(track)
    length = min(len(track) - first, len(stereo))
    track[first:first + length] += stereo[:length]
    if loop and length < len(stereo):
        track[:len(stereo) - length] += stereo[length:]


def save(name, track, rms, ceiling):
    scale = min(rms / np.sqrt(np.mean(track ** 2)), ceiling / np.max(np.abs(track)))
    pcm = np.rint(track * scale * 32767).astype("<i2")
    with wave.open(str(OUT / (name + ".wav")), "wb") as output:
        output.setparams((2, 2, RATE, 0, "NONE", "not compressed"))
        output.writeframes(pcm.tobytes())


def hz(note):
    return 440 * 2 ** ((note - 69) / 12)


def string(note, seconds, bass=False):
    t = timeline(seconds)
    f = hz(note)
    phase = TAU * f * (t + 0.00007 * (1 - np.exp(-t / 0.025)))
    out = np.zeros_like(t)
    for k in range(1, 8):
        color = np.sin(k * np.pi * (0.29 if bass else 0.22)) / k ** 1.55
        damping = (2.3 if bass else 2.7) + 0.56 * k
        out += color * np.sin(k * phase * (1 + 0.000015 * k * k)) * np.exp(-t * damping)
    out += 0.035 * noise(t, 730 if bass else 1360, 650) * np.exp(-t * 70)
    return out * (1 - np.exp(-t / 0.004)) * release(t, 0.08)


def flute(note, seconds):
    t = timeline(seconds)
    f = hz(note)
    drift = 0.0018 * np.clip((t - 0.12) * 4, 0, 1) * np.sin(TAU * 5.0 * t)
    phase = TAU * f * np.cumsum(1 + drift - 0.007 * np.exp(-t / 0.034)) / RATE
    body = (np.sin(phase) + 0.22 * np.sin(2 * phase + 0.16)
            + 0.09 * np.sin(3 * phase) + 0.027 * np.sin(5 * phase))
    breath = noise(t, f * 3.3, f * 1.3) * (0.021 + 0.07 * np.exp(-t / 0.045))
    return ((body + breath) * (1 - np.exp(-t / 0.037))
            * (0.84 + 0.16 * np.exp(-t / 0.13)) * release(t, 0.12))


def mallet(note, seconds):
    t = timeline(seconds)
    p = TAU * hz(note) * t
    body = (np.sin(p) * np.exp(-t * 5) + 0.32 * np.sin(3.98 * p) * np.exp(-t * 15)
            + 0.07 * np.sin(8.9 * p) * np.exp(-t * 34))
    return body * (1 - np.exp(-t / 0.003)) * release(t, 0.05)


def story_music():
    # Four eight-bar phrases in G. The last bar is a D7 pickup into bar one.
    chords = {
        "G": (43, [55, 59, 62, 67]), "D/F#": (42, [54, 57, 62, 66]),
        "Em": (40, [55, 59, 62, 64]), "Bm": (47, [54, 59, 62, 66]),
        "C": (48, [55, 60, 64, 67]), "G/B": (47, [55, 59, 62, 67]),
        "Am": (45, [55, 60, 64, 69]), "D7": (38, [54, 60, 62, 69]),
    }
    harmony = ("G D/F# Em Bm C G/B Am D7 G C Em D/F# C Am D7 G "
               "Em Bm C G/B Am Em D7 D7 G D/F# Em Bm C G/B Am D7").split()
    # Each phrase gives (start in beats, MIDI note, length in beats).
    melody = [
        [(0,71,1), (1.45,74,.52), (2,76,.82)], [(0,74,1.4), (1.55,69,.68)],
        [(.04,67,.88), (1,71,.80), (2,74,.78)], [(0,71,1.9)],
        [(.45,72,.47), (1,76,.94), (2,79,.74)], [(0,78,.73), (.8,74,.62), (1.5,71,1.1)],
        [(0,69,1), (1.4,72,.55), (2,71,.56)], [(0,69,1.35)],
        [(0,67,.8), (1,71,.85), (2,74,.8)], [(0,76,1.35), (1.55,72,.65)],
        [(.5,71,.46), (1,74,.85), (2,76,.72)], [(0,74,1.45), (2,69,.68)],
        [(0,72,1.2), (1.5,76,.65)], [(0,74,.7), (1,72,.82), (2,69,.78)],
        [(0,66,1), (1.46,69,.5), (2,74,.7)], [(0,67,1.75)],
        [(.5,71,.9), (1.5,74,.44), (2,76,.9)], [(0,78,1.34), (1.5,74,1.05)],
        [(0,79,1.3), (1.51,76,.45), (2,72,.8)], [(0,74,1.8)],
        [(.5,72,.72), (1.5,76,.45), (2,79,.7)], [(0,78,.84), (1,76,1.3)],
        [(0,74,.95), (1.46,72,.48), (2,69,.77)], [(0,66,1.36), (2,69,.52)],
        [(0,71,1), (1.47,74,.52), (2,76,.78)], [(0,74,1.4), (1.55,69,.68)],
        [(0,67,.82), (1,71,.80), (2,74,.78)], [(0,71,1.9)],
        [(0,72,.72), (1,76,.78), (2,79,.78)], [(0,78,.78), (1,74,.78), (2,71,.78)],
        [(0,69,.9), (1,72,.9), (2,76,.75)], [(0,74,1.1), (1.5,69,.52), (2.3,66,.52)],
    ]
    track = np.zeros((round(BAR * len(harmony) * RATE), 2))
    for i, chord in enumerate(harmony):
        start = i * BAR
        low, notes = chords[chord]
        add(track, string(low, 1.05, True), start, 0.40, -.08, True)
        if i % 4 in (1, 3):
            add(track, string(low + 7, .49, True), start + 2 * BEAT, .19, -.08, True)
        pattern = (0, 2, 1, 3, 2) if i % 2 else (0, 1, 2, 3, 1)
        for beat, idx in zip((.02,.52,1,1.53,2.25), pattern):
            moment = start + BEAT * beat + random.uniform(-.008, .008)
            add(track, string(notes[idx], .96), moment,
                random.uniform(.21,.28), -.40 + .06 * idx, True)
        for beat, note, length in melody[i]:
            add(track, flute(note, length * BEAT + .09), start + BEAT * beat + .018,
                random.uniform(.15,.18), .12, True)
        if i % 4 in (1, 3):
            for beat, idx in ((2.05,1), (2.55,2)):
                add(track, mallet(notes[idx] + 12, .48), start + beat * BEAT,
                    .081 if i < 16 else .10, .46, True)
        if 8 <= i < 28 and i % 2 == 0:
            for beat in (1.5, 2.55):
                t = timeline(.13)
                tap = noise(t, 3100, 1250) * np.sin(np.pi * t / .13) ** 3
                add(track, tap, start + beat * BEAT, .008, .32, True)
    # Circular room reflections preserve decaying notes through the loop seam.
    dry = track.copy()
    for delay, gain in ((.031,.11), (.067,.075), (.113,.055), (.193,.036), (.29,.024)):
        track += np.roll(dry[:, ::-1], round(RATE * delay), axis=0) * gain
    save("pasture", track, .090, .46)


def sound_effects():
    def blank(duration):
        return np.zeros((round(RATE * duration), 2))

    def brush(duration, center, width, attack=.3):
        t = timeline(duration)
        x = t / duration
        env = np.minimum(x / attack, 1) * np.minimum((1-x) / (1-attack), 1) ** 1.7
        return noise(t, center, width) * env * release(t, .015)

    def drop(duration, start, end, damping):
        t = timeline(duration)
        glide = (end - start) * (t + (np.exp(-t / .036)-1) * .036)
        phase = TAU * (start * t + glide)
        return (np.sin(phase) + .18 * np.sin(2 * phase)) * (1-np.exp(-t/.0025)) * np.exp(-t*damping) * release(t,.025)

    def finish(name, track, rms, peak):
        delay = round(.032 * RATE)
        track[delay:] += .085 * track[:-delay, ::-1].copy()
        track[-round(.025 * RATE):] *= np.linspace(1, 0, round(.025 * RATE))[:,None]
        save(name, track, rms, peak)

    # Hoe: short approach, low earth impact, then loose grains rolling away.
    track = blank(.64)
    add(track, brush(.18,1100,640,.7), .00, .20, -.24)
    add(track, drop(.29,135,54,15), .15, .95, .02)
    add(track, brush(.25,570,510,.065), .15, .58, .02)
    for at in (.23,.27,.33,.38):
        add(track, brush(.10,960,670,.09), at, random.uniform(.09,.18), random.uniform(-.3,.3))
    finish("till",track,.117,.56)

    # Sowing: dry seed packet and four small, differently sized seed taps.
    track = blank(.62)
    add(track, brush(.20,2200,810,.45), .01, .13, -.23)
    for at, pitch, pan in ((.16,850,-.23),(.235,1050,-.10),(.31,930,.12),(.405,1230,.26)):
        add(track, drop(.105,pitch,pitch*.72,40), at, .35, pan)
        add(track, brush(.048,2900,960,.1), at, .22, pan)
    finish("sow",track,.085,.41)

    # Water: droplets rise in pitch over a steady, airy watering-can stream.
    track = blank(1.10)
    add(track, brush(.88,1830,1150,.23), .045, .23, -.08)
    for at in np.arange(.055,.93,.037):
        f = random.uniform(620,1470)
        add(track, drop(.13, f, f*1.65, 37), at + random.uniform(-.014,.014),
            random.uniform(.12,.29), random.uniform(-.48,.48))
    finish("water",track,.109,.48)

    # Picking by hand: leaf snap, soft pluck and a short rising reward interval.
    track = blank(.78)
    add(track, brush(.13,1700,920,.62), .003,.38,-.14)
    add(track, drop(.14,370,198,28), .08,.48,-.06)
    add(track, mallet(79,.39), .095,.48,.09)
    add(track, mallet(83,.43), .225,.32,.22)
    finish("pick",track,.096,.46)

    # Sickle: broad pair of grass swishes, a quiet metallic edge and stem ticks.
    track = blank(.83)
    add(track, brush(.28,1800,900,.62), .01,.51,-.34)
    add(track, brush(.32,2550,1250,.53), .28,.60,.26)
    for at in (.194,.22,.41,.453,.496):
        add(track, brush(.051,1180,600,.08), at, .34, random.uniform(-.32,.32))
    add(track, mallet(91,.30), .43,.103,.18)
    finish("reap",track,.12,.52)

    # Fertilizer: paper pouch opening and a soft, descending handful of grains.
    track = blank(.89)
    add(track, brush(.21,980,480,.42), .012,.45,-.18)
    for i, at in enumerate(np.linspace(.22,.72,14)):
        add(track, brush(.063,random.uniform(950,2400),650,.12), at,
            .3 - i*.012, random.uniform(-.43,.43))
    add(track, mallet(69,.31), .42,.19,.05)
    finish("fertilize",track,.093,.43)


if __name__ == "__main__":
    story_music()
    sound_effects()
