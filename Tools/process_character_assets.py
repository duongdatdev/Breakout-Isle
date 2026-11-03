#!/usr/bin/env python3
"""Export QC-passed character sheets and write their Unity import contract."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import struct
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = ROOT / "ArtSource" / "Characters"
ASSET_ROOT = ROOT / "Assets" / "BreakoutIsle" / "Art" / "Characters"
DIRECTIONS = ["S", "SW", "W", "NW", "N", "NE", "E", "SE"]


def read_png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        raise ValueError(f"Not a valid PNG: {path}")
    return struct.unpack(">II", header[16:24])


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--character", required=True)
    parser.add_argument("--action", required=True)
    parser.add_argument("--frames", type=int, required=True)
    parser.add_argument("--fps", type=int, required=True)
    parser.add_argument("--loop", choices=["true", "false"], required=True)
    args = parser.parse_args()

    source = SOURCE_ROOT / args.character / args.action
    processed = source / "processed"
    meta_path = processed / "pipeline-meta.json"
    sheet_path = processed / "sheet-transparent.png"
    meta = json.loads(meta_path.read_text(encoding="utf-8"))
    qc = meta.get("qc_summary", {})
    failures = []
    if meta.get("rows") != len(DIRECTIONS) or meta.get("cols") != args.frames:
        failures.append("processor grid does not match direction rows and requested frame count")
    if meta.get("cell_size") != 64:
        failures.append("processor cell size is not 64px")
    for key in ("edge_touch_frames", "output_edge_touch_frames", "empty_frames", "paste_clamped_frames"):
        if meta.get(key):
            failures.append(f"{key} is non-empty")
    if qc.get("frame_count") != len(DIRECTIONS) * args.frames or qc.get("valid_frame_count") != len(DIRECTIONS) * args.frames:
        failures.append("frame count is incomplete")
    if qc.get("empty_count") or qc.get("edge_touch_count") or qc.get("paste_clamped_count"):
        failures.append("processor QC reports empty, edge-touching, or clamped frames")
    if not sheet_path.is_file():
        failures.append("transparent runtime sheet is missing")
    if failures:
        raise SystemExit("Refusing to export: " + "; ".join(failures))

    image_width, image_height = read_png_size(sheet_path)
    if (image_width, image_height) != (args.frames * 64, len(DIRECTIONS) * 64):
        raise SystemExit(f"Unexpected sheet size {image_width}x{image_height}; expected {args.frames * 64}x{len(DIRECTIONS) * 64}")

    output = ASSET_ROOT / args.character / args.action
    output.mkdir(parents=True, exist_ok=True)
    runtime_sheet = output / f"{args.action}.png"
    shutil.copy2(sheet_path, runtime_sheet)
    frame_ids = {
        direction: list(range(row * args.frames, (row + 1) * args.frames))
        for row, direction in enumerate(DIRECTIONS)
    }
    action_data = {
        "action": args.action,
        "sheet": runtime_sheet.name,
        "directionOrder": DIRECTIONS,
        "frameCount": args.frames,
        "fps": args.fps,
        "loop": args.loop == "true",
        "cellSizePx": 64,
        "pixelsPerUnit": 32,
        "pivot": {"x": 0.5, "y": 0.0625},
        "spriteImport": {
            "textureType": "Sprite (2D and UI)",
            "spriteMode": "Multiple",
            "filterMode": "Point",
            "mipmaps": False,
            "compression": "None",
            "meshType": "Full Rect",
            "slice": {"widthPx": 64, "heightPx": 64, "rowOrder": "top-to-bottom", "columnOrder": "left-to-right"},
        },
        "frames": frame_ids,
        "source": {
            "rawSheet": f"ArtSource/Characters/{args.character}/{args.action}/raw-sheet.png",
            "prompt": f"ArtSource/Characters/{args.character}/{args.action}/prompt-used.txt",
            "processorMetadata": f"ArtSource/Characters/{args.character}/{args.action}/processed/pipeline-meta.json",
            "rawSha256": sha256(source / "raw-sheet.png"),
        },
        "qc": qc,
    }
    serialized = json.dumps(action_data, indent=2) + "\n"
    (output / "action.json").write_text(serialized, encoding="utf-8")
    (output / "unity-import-settings.json").write_text(serialized, encoding="utf-8")
    print(f"Exported {args.character}/{args.action}: {image_width}x{image_height}, {qc['valid_frame_count']} QC-passed frames")


if __name__ == "__main__":
    main()
