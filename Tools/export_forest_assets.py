"""Export generated forest props and their gameplay/import metadata."""
import json
from pathlib import Path
import shutil

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource/Environment/Forest'
DESTINATION = ROOT / 'Assets/BreakoutIsle/Art/Environment/Forest/Props'
NAMES = ['hiding_bush', 'fern', 'moss_rock', 'stump', 'fallen_branch',
         'mushrooms', 'reeds', 'woodland_flowers', 'pebbles']


def main():
    DESTINATION.mkdir(parents=True, exist_ok=True)
    entries = [(name, SOURCE / 'Compact/processed' / f'prop-{i + 1}.png')
               for i, name in enumerate(NAMES)]
    entries.append(('broadleaf_tree', SOURCE / 'Tree/processed/prop-1.png'))
    for name in ('tent', 'truck'):
        source = SOURCE / 'Props' / name / 'processed/prop-1.png'
        if source.is_file():
            entries.append((name, source))
    props = []
    for name, source in entries:
        image = Image.open(source).convert('RGBA')
        assert image.getbbox() and image.getextrema()[3][0] == 0, name
        assert image.width == image.height and image.width in (64, 96, 128), name
        shutil.copyfile(source, DESTINATION / (name + '.png'))
        blocker = name in ('moss_rock', 'stump', 'broadleaf_tree', 'tent', 'truck')
        tall = name in ('hiding_bush', 'reeds', 'broadleaf_tree', 'tent', 'truck')
        props.append(dict(id=name, file=name + '.png', size=list(image.size),
                          pixelsPerUnit=32, pivot=[0.5, 0.0625], anchor='feet',
                          role='hiding_place' if name == 'hiding_bush' else 'blocker' if blocker else 'dressing',
                          occlusionPolicy='fade_when_occluding_player' if tall else 'normal'))
    animations = {}
    trap_source = SOURCE / 'Props/bear_trap/processed'
    if (trap_source / 'qc.json').is_file():
        for index in range(4):
            name = f'bear_trap_{index:02}'
            source = trap_source / (name + '.png')
            image = Image.open(source).convert('RGBA')
            assert image.size == (64, 64) and image.getbbox() and image.getextrema()[3][0] == 0
            shutil.copyfile(source, DESTINATION / (name + '.png'))
            props.append(dict(id=name, file=name + '.png', size=[64, 64], pixelsPerUnit=32,
                              pivot=[0.5, 0.5], anchor='center', role='hazard'))
        animations['bear_trap'] = dict(frames=[f'bear_trap_{i:02}' for i in range(4)],
                                       fps=10, loop=False, states=['armed', 'closing', 'snapped', 'spent'])
    (DESTINATION / 'props.json').write_text(json.dumps(dict(version=1, props=props, animations=animations), indent=2), encoding='utf-8')
    print(f'Exported {len(props)} generated forest props; transparent margins verified')


if __name__ == '__main__':
    main()
