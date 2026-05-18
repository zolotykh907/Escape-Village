import math
import os
import wave

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
AUDIO_DIR = os.path.join(ROOT, "Assets", "Resources", "Audio")
SAMPLE_RATE = 44100


def clamp(value):
    return max(-1.0, min(1.0, value))


def write_wav(name, duration, sample_func):
    path = os.path.join(AUDIO_DIR, name)
    total = int(SAMPLE_RATE * duration)
    with wave.open(path, "w") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        for i in range(total):
            t = i / SAMPLE_RATE
            value = int(clamp(sample_func(t, duration)) * 32767)
            output.writeframesraw(value.to_bytes(2, byteorder="little", signed=True))


def fade(t, duration, attack=0.04, release=0.12):
    a = min(1.0, t / attack) if attack > 0 else 1.0
    r = min(1.0, (duration - t) / release) if release > 0 else 1.0
    return max(0.0, min(a, r))


def ambient(t, duration):
    base = math.sin(math.tau * 110 * t) * 0.12
    fifth = math.sin(math.tau * 165 * t + 0.3) * 0.08
    shimmer = math.sin(math.tau * 440 * t + math.sin(t * 0.7) * 1.5) * 0.025
    pulse = 0.75 + 0.25 * math.sin(math.tau * 0.18 * t)
    return (base + fifth + shimmer) * pulse * fade(t, duration, 0.4, 0.4)


def pickup(t, duration):
    pitch = 520 + 520 * (t / duration)
    tone = math.sin(math.tau * pitch * t) * 0.35
    sparkle = math.sin(math.tau * 1560 * t) * 0.12
    return (tone + sparkle) * fade(t, duration, 0.01, 0.22)


def death(t, duration):
    pitch = 180 - 90 * (t / duration)
    tone = math.sin(math.tau * pitch * t) * 0.42
    grit = math.sin(math.tau * 47 * t) * 0.18
    return (tone + grit) * fade(t, duration, 0.02, 0.35)


def victory(t, duration):
    chord = (
        math.sin(math.tau * 392 * t)
        + math.sin(math.tau * 494 * t)
        + math.sin(math.tau * 659 * t)
    ) / 3.0
    bell = math.sin(math.tau * 1318 * t) * 0.14
    return (chord * 0.42 + bell) * fade(t, duration, 0.03, 0.5)


os.makedirs(AUDIO_DIR, exist_ok=True)
write_wav("AmbientLoop.wav", 8.0, ambient)
write_wav("ArtifactPickup.wav", 0.9, pickup)
write_wav("DeathSting.wav", 1.2, death)
write_wav("VictorySting.wav", 1.8, victory)
