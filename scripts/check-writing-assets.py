#!/usr/bin/env python3
"""Check bundled writing assets using only the Python standard library."""
import hashlib
import json
from pathlib import Path
import wave

root = Path(__file__).resolve().parent.parent
assets = root / 'apps/web/public/writing'
manifest = json.loads((assets / 'manifest.json').read_text())
for relative, expected in manifest['sha256'].items():
    path = assets / relative
    assert path.is_file(), f'Missing asset: {relative}'
    assert hashlib.sha256(path.read_bytes()).hexdigest() == expected, f'Changed asset: {relative}'
characters = 0
for path in (root / 'packs').glob('*.json'):
    source = json.loads(path.read_text())
    for character in source.get('pack', source).get('characters', []):
        audio = root / 'apps/web/public' / character['audioUrl'].lstrip('/')
        with wave.open(str(audio)) as clip:
            assert clip.getnframes() > 0, f'Empty audio: {audio}'
        assert audio.relative_to(assets).as_posix() in manifest['sha256']
        data = character['strokeData']
        assert len(data['strokes']) == len(data['medians']) > 0
        characters += 1
print(f'PASS: {characters} writing characters; {len(manifest["sha256"])} bundled assets verified.')
