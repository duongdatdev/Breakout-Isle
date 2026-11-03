"""Verify delivered directional animations contain usable, distinct runtime frames."""
import hashlib
import json
from pathlib import Path
import re
import sys

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
CHARACTERS = ROOT / 'Assets/BreakoutIsle/Art/Characters'
DIRECTIONS = ['S', 'SW', 'W', 'NW', 'N', 'NE', 'E', 'SE']
REQUIRED = {
    'RookPrison': ['idle', 'walk', 'run', 'sneak', 'interact', 'throw',
                   'hide-enter', 'hide-exit', 'caught', 'crawl', 'pry', 'climb'],
    'Guard': ['idle', 'walk', 'run', 'search', 'capture'],
}

def main():
    failures = []
    reports = []
    for character, actions in REQUIRED.items():
        for action in actions:
            folder = CHARACTERS / character / action
            manifest_path = folder / 'action.json'
            if not manifest_path.exists():
                failures.append(character + '/' + action + ': missing manifest')
                continue
            manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
            sheet_path = folder / manifest['sheet']
            image = Image.open(sheet_path).convert('RGBA')
            frames = manifest['frameCount']
            cell = manifest['cellSizePx']
            identity = character + '/' + action
            if manifest['directionOrder'] != DIRECTIONS:
                failures.append(identity + ': incorrect direction order')
            if image.size != (cell * frames, cell * len(DIRECTIONS)):
                failures.append(identity + ': sheet dimensions do not match manifest')
            if cell != 64 or manifest['pixelsPerUnit'] != 32 or manifest['fps'] <= 0:
                failures.append(identity + ': incorrect cell/PPU/frame rate')
            distinct = {}
            row_hashes = set()
            for row, direction in enumerate(DIRECTIONS):
                frame_hashes = set()
                for frame in range(frames):
                    sprite = image.crop((frame * cell, row * cell, (frame + 1) * cell, (row + 1) * cell))
                    if not sprite.getbbox():
                        failures.append(identity + '/' + direction + ': empty frame ' + str(frame))
                    frame_hashes.add(hashlib.sha256(sprite.tobytes()).hexdigest())
                distinct[direction] = len(frame_hashes)
                if len(frame_hashes) < 2:
                    failures.append(identity + '/' + direction + ': no pixel changes across animation')
                strip = image.crop((0, row * cell, image.width, (row + 1) * cell))
                row_hashes.add(hashlib.sha256(strip.tobytes()).hexdigest())
            if len(row_hashes) != 8:
                failures.append(identity + ': duplicate direction rows')
            meta_path = Path(str(sheet_path) + '.meta')
            if not meta_path.exists():
                failures.append(identity + ': Unity import metadata missing')
            else:
                meta = meta_path.read_text(encoding='utf-8-sig')
                for pattern, description in [(r'spriteMode:\s*2\b', 'Multiple sprite mode'),
                        (r'spritePixelsToUnits:\s*32(?:\.0+)?\b', '32 PPU'),
                        (r'filterMode:\s*0\b', 'Point filtering'),
                        (r'enableMipMap:\s*0\b', 'disabled mipmaps')]:
                    if not re.search(pattern, meta):
                        failures.append(identity + ': expected ' + description)
            reports.append(dict(character=character, action=action, frames=frames * 8,
                                uniqueFramesPerDirection=distinct))
    result = {'actions': len(reports), 'frames': sum(x['frames'] for x in reports),
              'failures': failures, 'report': reports}
    print(json.dumps(result, indent=2))
    return 1 if failures else 0

if __name__ == '__main__':
    sys.exit(main())
