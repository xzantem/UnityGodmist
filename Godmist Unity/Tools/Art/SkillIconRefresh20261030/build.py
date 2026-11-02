"""Original full-canvas nearest Aseprite export. No BOX/crop/quantization."""
from pathlib import Path
import hashlib
import json
import subprocess

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
DESTINATIONS = {
    'discharge': 'Skills/VS22/discharge-32.png',
    'wounding': 'Passives/VS22/WarriorBrawler/wounding-32.png',
    'larger_wound': 'Passives/VS22/WarriorBrawler/larger_wound-32.png',
    'revenge': 'Passives/VS22/WarriorBrawler/revenge-32.png',
    'efficient_recovery': 'Passives/VS22/ScoutBladeDancer/efficient_recovery-32.png',
}


def build():
    (ROOT / 'native').mkdir(exist_ok=True)
    ase = 'C:/Program Files/Aseprite.v1.3.17/game/Aseprite.exe'
    subprocess.run([ase, '-b', '--script-param', 'root=' + ROOT.as_posix() + '/',
                    '--script', str(ROOT / 'export.lua')], check=True)
    records = []
    for slug, relative in DESTINATIONS.items():
        native = ROOT / 'native' / (slug + '-32.png')
        dest = PROJECT / 'Assets/_Project/Art/Icons' / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(native.read_bytes())
        records.append({'slug': slug, 'destination': dest.relative_to(PROJECT).as_posix(),
            'sourceSha256': hashlib.sha256((ROOT / 'sources' / (slug + '.png')).read_bytes()).hexdigest(),
            'outputSha256': hashlib.sha256(native.read_bytes()).hexdigest()})
    (ROOT / 'records.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
    print('Exported five full-canvas nearest32 PNG/Aseprite icons.')


if __name__ == '__main__':
    build()
