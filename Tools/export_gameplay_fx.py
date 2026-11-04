"""Export QC-passed generated gameplay effects as ordered single-sprite frames."""
import hashlib
import json
from pathlib import Path
import shutil

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]


def main():
    for name in ('stone_impact', 'pickup_glimmer'):
        source = ROOT / 'ArtSource/FX' / name
        qc_path = source / 'processed/qc.json'
        qc = json.loads(qc_path.read_text(encoding='utf-8'))
        assert qc['size'] == 32 and len(qc['frames']) == 4, name
        destination = ROOT / 'Assets/BreakoutIsle/Art/FX' / name
        frames = []
        for index, entry in enumerate(qc['frames']):
            file = source / 'processed' / entry['file']
            frame = Image.open(file).convert('RGBA')
            bounds = frame.getbbox()
            assert frame.size == (32, 32) and bounds, (name, index)
            assert min(bounds[:2]) > 0 and max(bounds[2:]) < 32, (name, index, bounds)
            assert hashlib.sha256(frame.tobytes()).hexdigest() == entry['sha256']
            frames.append(file)
        assert len({entry['sha256'] for entry in qc['frames']}) == 4, name
        assert (source / 'prompt-used.txt').read_text(encoding='utf-8-sig').strip(), name
        destination.mkdir(parents=True, exist_ok=True)
        for frame in frames:
            shutil.copyfile(frame, destination / frame.name)
        manifest = dict(effect=name, frames=[frame.name for frame in frames], fps=10, loop=False,
                        pixelsPerUnit=32, pivot=[0.5, 0.5], filter='Point', mipmaps=False,
                        compression='None', artSource='built-in image_gen',
                        sourcePrompt=str((source / 'prompt-used.txt').relative_to(ROOT)).replace('\\', '/'),
                        sourceQc=str(qc_path.relative_to(ROOT)).replace('\\', '/'))
        (destination / 'effect.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
        print(f'{name}: exported four distinct nonempty 32px frames')


if __name__ == '__main__':
    main()
