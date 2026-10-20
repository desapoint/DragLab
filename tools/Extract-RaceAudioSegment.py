"""Crop a PCM16 race audition and optionally repeat it with silent gaps.

Only a short edge fade is applied; gain and the interior samples are unchanged.
"""
import argparse
from array import array
from pathlib import Path
import sys
import wave


def extract(source, output, start, end, repeats=1, gap=0.4):
    if start < 0 or end <= start or repeats < 1 or gap < 0:
        raise ValueError("Use a positive interval, repeat count and nonnegative gap.")
    with wave.open(str(source), "rb") as reader:
        rate, channels = reader.getframerate(), reader.getnchannels()
        if reader.getsampwidth() != 2 or reader.getcomptype() != "NONE":
            raise ValueError("Input must be uncompressed PCM16 WAV.")
        first, last = round(start * rate), round(end * rate)
        if last > reader.getnframes():
            raise ValueError("Segment extends beyond the recording.")
        reader.setpos(first)
        raw = reader.readframes(last - first)
    pcm = array("h")
    pcm.frombytes(raw)
    if sys.byteorder != "little":
        pcm.byteswap()
    frames = last - first
    fade = min(round(rate * 0.005), frames // 2)
    for frame in range(fade):
        gain = frame / max(1, fade - 1)
        for channel in range(channels):
            for at in (frame * channels + channel,
                       (frames - 1 - frame) * channels + channel):
                pcm[at] = round(pcm[at] * gain)
    if sys.byteorder != "little":
        pcm.byteswap()
    payload = pcm.tobytes()
    silence = bytes(round(gap * rate) * channels * 2)
    output.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(output), "wb") as writer:
        writer.setnchannels(channels)
        writer.setsampwidth(2)
        writer.setframerate(rate)
        for repetition in range(repeats):
            if repetition:
                writer.writeframes(silence)
            writer.writeframes(payload)
    print(f"{output.resolve()} / {start:.3f} to {end:.3f}s / {repeats} repeats")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--start", type=float, required=True)
    parser.add_argument("--end", type=float, required=True)
    parser.add_argument("--repeats", type=int, default=1)
    parser.add_argument("--gap", type=float, default=0.4)
    args = parser.parse_args()
    if args.source.resolve() == args.output.resolve():
        parser.error("Output must not overwrite the source.")
    extract(args.source, args.output, args.start, args.end, args.repeats, args.gap)
