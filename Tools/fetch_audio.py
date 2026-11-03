"""Fetch and curate verified CC0 audio from its original publishers."""
from pathlib import Path
import json
import re
import subprocess
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / 'Temp' / 'BreakoutAudioDownloads'
OUT = ROOT / 'Assets' / 'BreakoutIsle' / 'Audio'
SOURCES = [
    ('interface', 'Kenney', 'https://kenney.nl/assets/interface-sounds', 'https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip'),
    ('impact', 'Kenney', 'https://kenney.nl/assets/impact-sounds', 'https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip'),
    ('rpg', 'Kenney', 'https://kenney.nl/assets/rpg-audio', 'https://kenney.nl/media/pages/assets/rpg-audio/8e99002d76-1677590336/kenney_rpg-audio.zip'),
    ('stealth_woods', 'Lisboa', 'https://opengameart.org/content/stealth-music', 'https://opengameart.org/sites/default/files/Stealth%20in%20the%20Woods_0.mp3'),
    ('infiltration', 'Adiutorium', 'https://opengameart.org/content/infiltration', 'https://opengameart.org/sites/default/files/infiltration_2.mp3'),
    ('dog_bark', 'Brandon Morris (submitted by HaelDB)', 'https://opengameart.org/content/dog-barking-mono', 'https://opengameart.org/sites/default/files/dog_barking_mono.wav'),
    ('alarm', 'EZduzziteh', 'https://opengameart.org/content/alarm-1', 'https://opengameart.org/sites/default/files/alarm.ogg'),
    ('water_splash_1', 'Peludo', 'https://opengameart.org/content/water-splash-and-sand-footsteps', 'https://opengameart.org/sites/default/files/splash1_0.wav'),
    ('water_splash_2', 'Peludo', 'https://opengameart.org/content/water-splash-and-sand-footsteps', 'https://opengameart.org/sites/default/files/splash2_0.wav'),
    ('forest_ambience', 'TinyWorlds', 'https://opengameart.org/content/forest-ambience', 'https://opengameart.org/sites/default/files/Forest_Ambience.mp3'),
    ('prison_ambience', 'JaggedStone', 'https://opengameart.org/content/loopable-dungeon-ambience', 'https://opengameart.org/sites/default/files/dungeon_ambient_1.ogg'),
    ('city_ambience', 'qubodup', 'https://freesound.org/people/qubodup/sounds/223093/', 'https://cdn.freesound.org/previews/223/223093_71257-hq.mp3'),
    ('heartbeat', 'Independent.nu (submitted by qubodup)', 'https://opengameart.org/content/heartbeat-single-sound', 'https://opengameart.org/sites/default/files/heartbeat.mp3_.flac'),
    ('firework_pack', 'rubberduck', 'https://opengameart.org/content/25-cc0-bang-firework-sfx', 'https://opengameart.org/sites/default/files/25-CC0-bang-sfx.zip'),
    ('different_steps', 'TinyWorlds', 'https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud', 'https://opengameart.org/sites/default/files/%5Bkdd%5DDifferentSteps_0.zip'),
]

# Only these source files are carried into the Unity project. Output filename -> source id/path.
KENNEY_FILES = {
    'impact': {
        'Footsteps/step_concrete_01.ogg': 'Audio/footstep_concrete_000.ogg',
        'Footsteps/step_concrete_02.ogg': 'Audio/footstep_concrete_002.ogg',
        'Footsteps/step_concrete_03.ogg': 'Audio/footstep_concrete_004.ogg',
        'Footsteps/step_grass_01.ogg': 'Audio/footstep_grass_000.ogg',
        'Footsteps/step_grass_02.ogg': 'Audio/footstep_grass_002.ogg',
        'Footsteps/step_grass_03.ogg': 'Audio/footstep_grass_004.ogg',
        'Footsteps/step_wood_01.ogg': 'Audio/footstep_wood_000.ogg',
        'Footsteps/step_wood_02.ogg': 'Audio/footstep_wood_002.ogg',
        'Footsteps/step_wood_03.ogg': 'Audio/footstep_wood_004.ogg',
        'Impacts/impact_glass_light.ogg': 'Audio/impactGlass_light_000.ogg',
        'Impacts/impact_metal_light.ogg': 'Audio/impactMetal_light_000.ogg',
        'Impacts/impact_wood_light.ogg': 'Audio/impactWood_light_000.ogg',
        'Impacts/impact_generic_light.ogg': 'Audio/impactGeneric_light_000.ogg',
    },
    'interface': {
        'UI/ui_click.ogg': 'Audio/click_001.ogg',
        'UI/ui_confirm.ogg': 'Audio/confirmation_001.ogg',
        'UI/ui_cancel.ogg': 'Audio/back_001.ogg',
        'UI/ui_error.ogg': 'Audio/error_001.ogg',
        'UI/ui_open.ogg': 'Audio/open_001.ogg',
        'UI/ui_switch.ogg': 'Audio/switch_001.ogg',
        'Impacts/glass_break.ogg': 'Audio/glass_001.ogg',
    },
    'rpg': {
        'Interactions/door_open.ogg': 'Audio/doorOpen_1.ogg',
        'Interactions/door_close.ogg': 'Audio/doorClose_1.ogg',
        'Interactions/metal_latch.ogg': 'Audio/metalLatch.ogg',
        'Interactions/metal_click.ogg': 'Audio/metalClick.ogg',
        'Interactions/item_coins.ogg': 'Audio/handleCoins.ogg',
        'Interactions/item_paper.ogg': 'Audio/bookFlip1.ogg',
    },
    'different_steps': {
        'Footsteps/step_stone_01.ogg': 'stone01.ogg',
        'Footsteps/step_leaves_01.ogg': 'leaves01.ogg',
        'Footsteps/step_leaves_02.ogg': 'leaves02.ogg',
        'Footsteps/step_gravel_01.ogg': 'gravel.ogg',
        'Footsteps/step_mud_01.ogg': 'mud02.ogg',
    },
}

OGA_FILES = {
    'dog_bark': ('Animals/dog_bark.wav', None),
    'alarm': ('Alerts/alarm.ogg', None),
    'water_splash_1': ('Water/water_splash_01.wav', None),
    'water_splash_2': ('Water/water_splash_02.wav', None),
    'forest_ambience': ('Ambience/forest_ambience.mp3', None),
    'prison_ambience': ('Ambience/prison_ambience.ogg', None),
    'city_ambience': ('Ambience/city_ambience.mp3', None),
    'heartbeat': ('Alerts/heartbeat.wav', 'heartbeat.mp3_.flac'),
    'stealth_woods': ('Music/stealth_woods.mp3', None),
    'infiltration': ('Music/infiltration.mp3', None),
}


def fetch(url, page, destination):
    if destination.exists() and destination.stat().st_size:
        return
    request = urllib.request.Request(url, headers={'User-Agent': 'BreakoutIsleAssetFetcher/1.0', 'Referer': page})
    with urllib.request.urlopen(request, timeout=60) as response:
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(response.read())


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    records = []
    local = {}
    for key, author, page, url in SOURCES:
        suffix = '.zip' if url.endswith('.zip') else Path(urllib.request.url2pathname(url.split('/')[-1])).suffix
        if '?' in suffix:
            suffix = '.mp3'
        destination = CACHE / (key + suffix)
        if key in ('interface', 'impact', 'rpg'):
            with urllib.request.urlopen(page, timeout=45) as response:
                html = response.read().decode('utf-8')
            download_id = re.search(r"data-download-id='([^']+)'", html).group(1)
            request = urllib.request.Request('https://kenney.nl/download-link/' + download_id, headers={'Referer': page})
            with urllib.request.urlopen(request, timeout=45) as response:
                url = json.load(response)['url']
        if not destination.exists() or (destination.suffix == '.zip' and not zipfile.is_zipfile(destination)):
            fetch(url, page, destination)
        local[key] = destination
        records.append({'id': key, 'author': author, 'page': page, 'download': url, 'license': 'CC0-1.0', 'verified': '2026-11-03'})

    OUT.mkdir(parents=True, exist_ok=True)
    imported = {}
    for pack, files in KENNEY_FILES.items():
        with zipfile.ZipFile(local[pack]) as archive:
            for output, source in files.items():
                data = archive.read(source)
                target = OUT / output
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
                imported[output.replace('\\', '/')] = {'source': pack, 'file': source}
    with zipfile.ZipFile(local['firework_pack']) as archive:
        members = archive.namelist()
        firework = next(name for name in members if Path(name).name.lower() == 'fw_01.ogg')
        output = 'Impacts/firecracker_pop_01.ogg'
        target = OUT / output
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(archive.read(firework))
        imported[output] = {'source': 'firework_pack', 'file': firework}

    for key, (output, _) in OGA_FILES.items():
        target = OUT / output
        target.parent.mkdir(parents=True, exist_ok=True)
        source_name = OGA_FILES[key][1]
        if source_name:
            subprocess.run(['ffmpeg', '-y', '-v', 'error', '-i', str(local[key]), '-c:a', 'pcm_s16le', str(target)], check=True)
            imported[output] = {'source': key, 'source_file': source_name, 'conversion': 'FLAC to PCM s16le WAV; lossless; ffmpeg'}
        else:
            target.write_bytes(local[key].read_bytes())
            imported[output] = {'source': key}

    licenses = OUT / 'Licenses'
    licenses.mkdir(exist_ok=True)
    for pack in ('interface', 'impact', 'rpg'):
        with zipfile.ZipFile(local[pack]) as archive:
            license_file = next(name for name in archive.namelist() if Path(name).name.lower() == 'license.txt')
            (licenses / ('Kenney-' + pack + '-License.txt')).write_bytes(archive.read(license_file))
    fetch('https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt', 'https://creativecommons.org/publicdomain/zero/1.0/', licenses / 'CC0-1.0.txt')
    (OUT / 'AudioManifest.json').write_text(json.dumps({
        'version': 1,
        'events': {
            'player.step.prison': ['Footsteps/step_concrete_01.ogg', 'Footsteps/step_concrete_02.ogg', 'Footsteps/step_concrete_03.ogg', 'Footsteps/step_stone_01.ogg'],
            'player.step.forest': ['Footsteps/step_grass_01.ogg', 'Footsteps/step_grass_02.ogg', 'Footsteps/step_grass_03.ogg', 'Footsteps/step_leaves_01.ogg', 'Footsteps/step_leaves_02.ogg'],
            'player.step.wood': ['Footsteps/step_wood_01.ogg', 'Footsteps/step_wood_02.ogg', 'Footsteps/step_wood_03.ogg'],
            'player.step.gravel': ['Footsteps/step_gravel_01.ogg'],
            'player.step.mud': ['Footsteps/step_mud_01.ogg'],
            'player.step.water': ['Water/water_splash_01.wav', 'Water/water_splash_02.wav'],
            'interaction.door.open': ['Interactions/door_open.ogg'],
            'interaction.door.close': ['Interactions/door_close.ogg'],
            'interaction.switch': ['UI/ui_switch.ogg'],
            'interaction.lock': ['Interactions/metal_latch.ogg', 'Interactions/metal_click.ogg'],
            'hazard.bear_trap.snap': ['Interactions/metal_latch.ogg', 'Impacts/impact_metal_light.ogg'],
            'interaction.pickup': ['UI/ui_confirm.ogg', 'Interactions/item_coins.ogg'],
            'interaction.paper': ['Interactions/item_paper.ogg'],
            'ui.click': ['UI/ui_click.ogg'], 'ui.confirm': ['UI/ui_confirm.ogg'],
            'ui.cancel': ['UI/ui_cancel.ogg'], 'ui.error': ['UI/ui_error.ogg'],
            'ui.panel.open': ['UI/ui_open.ogg'], 'ui.checkpoint.saved': ['UI/ui_confirm.ogg'],
            'alert.guard': ['Alerts/alarm.ogg'], 'alert.heartbeat': ['Alerts/heartbeat.wav'],
            'animal.dog.bark': ['Animals/dog_bark.wav'],
            'throw.impact.soft': ['Impacts/impact_generic_light.ogg'],
            'throw.impact.wood': ['Impacts/impact_wood_light.ogg'],
            'throw.impact.metal': ['Impacts/impact_metal_light.ogg'],
            'throw.impact.glass': ['Impacts/impact_glass_light.ogg', 'Impacts/glass_break.ogg'],
            'throw.firecracker': ['Impacts/firecracker_pop_01.ogg'],
            'ambience.prison': ['Ambience/prison_ambience.ogg'],
            'ambience.forest': ['Ambience/forest_ambience.mp3'],
            'ambience.city': ['Ambience/city_ambience.mp3'],
            'music.stealth': ['Music/stealth_woods.mp3'],
            'music.chase': ['Music/infiltration.mp3'],
        },
        'assets': imported,
    }, indent=2), encoding='utf-8')
    (CACHE / 'sources.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
    print(f'Imported {len(imported)} source files into {OUT}')


if __name__ == '__main__':
    main()
