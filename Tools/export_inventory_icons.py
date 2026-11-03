"""Export named inventory sprites from the QC'd generated compact-item pack."""
import json
from pathlib import Path
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource/UI/Inventory/processed'
DESTINATION = ROOT / 'Assets/BreakoutIsle/Art/UI/Icons'
ITEMS = ['spoon', 'keycard', 'brass_key', 'wire_cutters', 'shovel', 'map_fragment',
         'cipher_note', 'civilian_clothes', 'stone', 'tin_can', 'glass_bottle',
         'firecracker', 'meat', 'energy_drink', 'boat_key', 'port_pass']

def main():
    DESTINATION.mkdir(parents=True, exist_ok=True)
    records = []
    for index, name in enumerate(ITEMS, start=1):
        source = SOURCE / ('icon-' + str(index) + '.png')
        image = Image.open(source)
        assert image.size == (32, 32) and image.mode == 'RGBA' and image.getbbox(), name
        shutil.copyfile(source, DESTINATION / (name + '.png'))
        records.append(dict(id=name, file=name + '.png', size=[32, 32], pixelsPerUnit=32))
    (DESTINATION / 'icons.json').write_text(json.dumps({'version': 1, 'icons': records}, indent=2), encoding='utf-8')
    print('Exported ' + str(len(records)) + ' named generated inventory icons')

if __name__ == '__main__':
    main()
