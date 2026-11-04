"""Export accepted generated city tiles and prop states with Unity metadata."""
import json
from pathlib import Path
import shutil

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource/Environment/City'
DESTINATION = ROOT / 'Assets/BreakoutIsle/Art/Environment/City'


def main():
    tiles = DESTINATION / 'Tiles'
    props = DESTINATION / 'Props'
    tiles.mkdir(parents=True, exist_ok=True)
    props.mkdir(parents=True, exist_ok=True)
    for source in (SOURCE / 'processed').iterdir():
        if source.suffix in ('.png', '.json'):
            shutil.copyfile(source, tiles / source.name)
    records, animations = [], {}
    for kind, size, anchor, pivot, loop in [
            ('valve', 64, 'center', [0.5, 0.5], False),
            ('boat', 128, 'center', [0.5, 0.5], True)]:
        folder = SOURCE / 'Props' / kind / 'processed'
        if not (folder / 'qc.json').is_file():
            continue
        names = []
        for index in range(4):
            name = f'{kind}_{index:02}'
            source = folder / (name + '.png')
            image = Image.open(source).convert('RGBA')
            assert image.size == (size, size) and image.getbbox()
            assert image.getextrema()[3][0] == 0, name
            shutil.copyfile(source, props / (name + '.png'))
            names.append(name)
            records.append(dict(id=name, file=name + '.png', size=list(image.size),
                                pixelsPerUnit=32, pivot=pivot, anchor=anchor,
                                role='exit' if kind == 'boat' else 'interactable'))
        animations[kind] = dict(frames=names, fps=8, loop=loop)
    (props / 'props.json').write_text(json.dumps(dict(version=1, props=records,
        animations=animations), indent=2), encoding='utf-8')
    print(f'Exported sixteen city foundation tiles and {len(records)} accepted prop states')


if __name__ == '__main__':
    main()
