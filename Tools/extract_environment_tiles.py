"""Deterministically extract generated art; never draw or invent terrain pixels."""
import argparse
import json
from pathlib import Path
from PIL import Image

NAMES = [
    'floor_clean', 'floor_cracked', 'floor_damp', 'floor_scuffed',
    'wall_clean', 'wall_cracked', 'wall_damp', 'wall_scuffed',
    'floor_drain', 'floor_dark', 'floor_utility', 'floor_warning',
    'boundary_left', 'boundary_right', 'boundary_top', 'boundary_bottom',
]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    args = parser.parse_args()
    args.destination.mkdir(parents=True, exist_ok=True)
    image = Image.open(args.source).convert('RGBA')
    tiles = []
    atlas = Image.new('RGBA', (128, 128))
    for index, name in enumerate(NAMES):
        row, col = divmod(index, 4)
        crop = image.crop((round(col * image.width / 4), round(row * image.height / 4),
                           round((col + 1) * image.width / 4), round((row + 1) * image.height / 4)))
        tile = crop.resize((32, 32), Image.Resampling.NEAREST)
        tile.save(args.destination / (name + '.png'))
        atlas.paste(tile, (col * 32, row * 32))
        tiles.append({'id': name, 'file': name + '.png', 'sourceCell': [col, row],
                      'size': [32, 32], 'pixelsPerUnit': 32,
                      'collider': 'cell' if name.startswith(('wall_', 'boundary_')) else 'none'})
    atlas.save(args.destination / 'prison-tiles-atlas.png')
    manifest = {'source': args.source.name, 'rows': 4, 'columns': 4, 'tiles': tiles,
                'artSource': 'built-in image_gen', 'postprocess': 'cell crop and nearest-neighbor resize only'}
    (args.destination / 'tiles.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    assert len(tiles) == 16
    print('Extracted 16 generated 32px tiles; atlas 128x128')

if __name__ == '__main__':
    main()
