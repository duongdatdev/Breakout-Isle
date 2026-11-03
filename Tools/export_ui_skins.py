"""Export QC'd generated 9-slice widgets and their import contract."""
import json
from pathlib import Path
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource/UI/Skins/processed'
DESTINATION = ROOT / 'Assets/BreakoutIsle/Art/UI/Skins'

def main():
    names = ['panel', 'slot_selected', 'button', 'warning']
    DESTINATION.mkdir(parents=True, exist_ok=True)
    for index, name in enumerate(names, start=1):
        frame = SOURCE / ('widget-' + str(index) + '.png')
        image = Image.open(frame)
        assert image.mode == 'RGBA' and image.size == (64, 64) and image.getbbox(), name
        shutil.copyfile(frame, DESTINATION / (name + '.png'))
    (DESTINATION / 'skins.json').write_text(json.dumps({'version': 1, 'size': [64, 64],
        'pixelsPerUnit': 32, 'border': [8, 8, 8, 8], 'skins': names}, indent=2), encoding='utf-8')
    print('Exported four generated UI skins, 64px with 8px slice borders')

if __name__ == '__main__':
    main()
