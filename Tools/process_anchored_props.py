"""Clean generated prop sheets while preserving their authored mount anchors."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    parser.add_argument('--name', required=True)
    parser.add_argument('--size', type=int, default=64)
    parser.add_argument('--pivot', nargs=2, type=float, default=[0.5, 0.5])
    args = parser.parse_args()
    args.destination.mkdir(parents=True, exist_ok=True)
    source = Image.open(args.source).convert('RGBA')
    pixels = np.asarray(source).copy()
    # Chroma-key only: retain all original non-magenta artwork and placement.
    red, green, blue = pixels[:, :, 0].astype(int), pixels[:, :, 1].astype(int), pixels[:, :, 2].astype(int)
    keyed = (red > 120) & (blue > 120) & (green < 125) & (red - green > 70) & (blue - green > 70)
    pixels[keyed] = 0
    cleaned = Image.fromarray(pixels)
    cleaned.save(args.destination / 'raw-clean.png')
    frames = []
    records = []
    sheet = Image.new('RGBA', (args.size * 2, args.size * 2))
    for index in range(4):
        row, col = divmod(index, 2)
        box = (round(col * source.width / 2), round(row * source.height / 2),
               round((col + 1) * source.width / 2), round((row + 1) * source.height / 2))
        frame = cleaned.crop(box).resize((args.size, args.size), Image.Resampling.NEAREST)
        bounds = frame.getbbox()
        assert bounds and min(bounds[:2]) > 0 and max(bounds[2:]) < args.size, (index, bounds)
        filename = f'{args.name}_{index:02}.png'
        frame.save(args.destination / filename)
        sheet.paste(frame, (col * args.size, row * args.size))
        frames.append(frame)
        records.append(dict(file=filename, sourceBox=box, alphaBounds=bounds,
                            sha256=hashlib.sha256(frame.tobytes()).hexdigest()))
    assert len({record['sha256'] for record in records}) == 4, 'Duplicate states'
    sheet.save(args.destination / 'sheet-transparent.png')
    frames[0].save(args.destination / 'preview.gif', save_all=True, append_images=frames[1:],
                   duration=180, loop=0, disposal=2)
    metadata = dict(source=str(args.source), artSource='built-in image_gen',
                    processing='magenta chroma key, fixed-cell crop and nearest-neighbor resize; no recentering',
                    rows=2, columns=2, size=args.size, pixelsPerUnit=32, pivot=args.pivot, frames=records)
    (args.destination / 'qc.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    print(f'{args.name}: four nonempty distinct states, transparent margins, fixed authored anchor')


if __name__ == '__main__':
    main()
