"""Copy QC'd generated prop frames into the Unity runtime asset directory."""
import json
from pathlib import Path
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource/Environment/Prison/Props'
DESTINATION = ROOT / 'Assets/BreakoutIsle/Art/Environment/Prison/Props'

def main():
    DESTINATION.mkdir(parents=True, exist_ok=True)
    exports = [('bed', 'prop-1.png', 'bed', 'feet'), ('desk', 'prop-1.png', 'desk', 'feet'),
               ('locker', 'state-1.png', 'locker_closed', 'feet'),
               ('locker', 'state-3.png', 'locker_open', 'feet')]
    for kind in ('gate', 'vent'):
        exports += [(kind, 'state-' + str(i + 1) + '.png', kind + '_' + str(i).zfill(2),
                     'center' if kind == 'vent' else 'feet') for i in range(4)]
    dressing_source = ROOT / 'ArtSource/Environment/Prison/Compact/processed'
    dressing_names = ['pillow', 'blanket_roll', 'mess_tray', 'tin_mug', 'bucket',
                      'soap', 'laundry_bundle', 'paper_ball', 'rag']
    props = []
    for source_name, frame, name, anchor in exports:
        source = SOURCE / source_name / 'processed' / frame
        image = Image.open(source)
        assert image.mode == 'RGBA' and image.getbbox(), name
        assert image.getextrema()[3][0] == 0, name + ' must have transparent padding'
        shutil.copyfile(source, DESTINATION / (name + '.png'))
        props.append(dict(id=name, file=name + '.png', size=list(image.size), pixelsPerUnit=32,
                          anchor=anchor, pivot=[0.5, 0.5] if anchor == 'center' else [0.5, 0.0625]))
    dressing_destination = DESTINATION / 'Dressing'
    dressing_destination.mkdir(exist_ok=True)
    for index, name in enumerate(dressing_names, start=1):
        source = dressing_source / ('prop-' + str(index) + '.png')
        image = Image.open(source)
        assert image.mode == 'RGBA' and image.size == (32, 32) and image.getbbox(), name
        shutil.copyfile(source, dressing_destination / (name + '.png'))
        props.append(dict(id=name, file='Dressing/' + name + '.png', size=[32, 32],
                          pixelsPerUnit=32, anchor='feet', pivot=[0.5, 0.0625]))
    (DESTINATION / 'props.json').write_text(json.dumps({'version': 1, 'props': props,
        'animations': {'gate': {'frames': ['gate_00','gate_01','gate_02','gate_03'], 'fps': 8, 'loop': False},
                       'vent': {'frames': ['vent_00','vent_01','vent_02','vent_03'], 'fps': 8, 'loop': False}}}, indent=2), encoding='utf-8')
    print('Exported ' + str(len(props)) + ' generated prison prop/state frames')

if __name__ == '__main__':
    main()
