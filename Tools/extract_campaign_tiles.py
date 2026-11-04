"""Extract generated terrain tiles with explicit biome identifiers and QC."""
import argparse
import json
from pathlib import Path

from PIL import Image

FOREST = ['grass_dark', 'grass_worn', 'leaf_earth', 'earth_path',
          'mud', 'water_shallow', 'water_deep', 'moss_paving',
          'grass_left', 'grass_right', 'grass_top', 'grass_bottom',
          'shore_left', 'shore_right', 'shore_top', 'shore_bottom']
CITY = ['cobblestone', 'asphalt', 'concrete', 'wood_planks',
        'sewer_floor', 'sewer_water', 'harbor_water', 'metal_deck',
        'brick_wall', 'corrugated_wall', 'sewer_wall', 'warehouse_wall',
        'curb_left', 'curb_right', 'curb_top', 'curb_bottom']


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    parser.add_argument('--biome', choices=['forest', 'city'], required=True)
    args = parser.parse_args()
    args.destination.mkdir(parents=True, exist_ok=True)
    source = Image.open(args.source).convert('RGBA')
    tiles = []
    atlas = Image.new('RGBA', (128, 128))
    for index, name in enumerate(FOREST if args.biome == 'forest' else CITY):
        row, col = divmod(index, 4)
        # Discard the thin generated cell separators, without inventing pixels.
        box = (round(col * source.width / 4) + 3, round(row * source.height / 4) + 3,
               round((col + 1) * source.width / 4) - 3, round((row + 1) * source.height / 4) - 3)
        tile = source.crop(box).resize((32, 32), Image.Resampling.NEAREST)
        assert tile.getbbox() == (0, 0, 32, 32), name
        tile.save(args.destination / (name + '.png'))
        atlas.paste(tile, (col * 32, row * 32))
        tiles.append(dict(id=name, file=name + '.png', size=[32, 32], pixelsPerUnit=32,
                          sourceBox=box, collider='none', terrain=name))
    atlas.save(args.destination / f'{args.biome}-tiles-atlas.png')
    (args.destination / 'tiles.json').write_text(json.dumps(dict(
        source=args.source.name, artSource='built-in image_gen', rows=4, columns=4, tiles=tiles,
        postprocess='fixed cell crop, 3px separator trim, nearest-neighbor downsample'), indent=2), encoding='utf-8')
    print(f'Exported {len(tiles)} nonempty generated foundation tiles')


if __name__ == '__main__':
    main()
