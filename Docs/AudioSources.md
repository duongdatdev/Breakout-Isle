# Audio Sources

Audio is imported under `Assets/BreakoutIsle/Audio`. The event-to-file map and per-file source IDs are in [AudioManifest.json](../Assets/BreakoutIsle/Audio/AudioManifest.json). The source archives are cached only under the ignored `Temp/BreakoutAudioDownloads` directory; the Unity project contains only the selected clips below.

All imported files are CC0 1.0. The applicable CC0 legal code is included at `Assets/BreakoutIsle/Audio/Licenses/CC0-1.0.txt`. Kenney's original package license files are preserved alongside it. License terms were checked on the original source pages on 2026-11-03.

## Source records

| Author | Original source | License | Imported files |
| --- | --- | --- | --- |
| Kenney | [Impact Sounds](https://kenney.nl/assets/impact-sounds) | CC0 1.0; original package terms in `Licenses/Kenney-impact-License.txt` | `Footsteps/step_concrete_01.ogg`, `step_concrete_02.ogg`, `step_concrete_03.ogg`, `step_grass_01.ogg`, `step_grass_02.ogg`, `step_grass_03.ogg`, `step_wood_01.ogg`, `step_wood_02.ogg`, `step_wood_03.ogg`; `Impacts/impact_glass_light.ogg`, `impact_metal_light.ogg`, `impact_wood_light.ogg`, `impact_generic_light.ogg` |
| Kenney | [Interface Sounds](https://kenney.nl/assets/interface-sounds) | CC0 1.0; original package terms in `Licenses/Kenney-interface-License.txt` | `UI/ui_click.ogg`, `ui_confirm.ogg`, `ui_cancel.ogg`, `ui_error.ogg`, `ui_open.ogg`, `ui_switch.ogg`; `Impacts/glass_break.ogg` |
| Kenney | [RPG Audio](https://kenney.nl/assets/rpg-audio) | CC0 1.0; original package terms in `Licenses/Kenney-rpg-License.txt` | `Interactions/door_open.ogg`, `door_close.ogg`, `metal_latch.ogg`, `metal_click.ogg`, `item_coins.ogg`, `item_paper.ogg` |
| TinyWorlds | [Different steps on wood, stone, leaves, gravel and mud](https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud) | CC0 | `Footsteps/step_stone_01.ogg`, `step_leaves_01.ogg`, `step_leaves_02.ogg`, `step_gravel_01.ogg`, `step_mud_01.ogg` |
| rubberduck | [25 CC0 bang / firework SFX](https://opengameart.org/content/25-cc0-bang-firework-sfx) | CC0 | `Impacts/firecracker_pop_01.ogg` (archive member `fw_01.ogg`) |
| Brandon Morris, submitted by HaelDB | [Dog barking mono](https://opengameart.org/content/dog-barking-mono) | CC0 (the page also lists OGA-BY 3.0; this project uses the separately listed CC0 grant) | `Animals/dog_bark.wav` (the mono recording) |
| EZduzziteh | [Alarm](https://opengameart.org/content/alarm-1) | CC0 | `Alerts/alarm.ogg` |
| Peludo | [Water Splash and sand footsteps](https://opengameart.org/content/water-splash-and-sand-footsteps); author profile links the requested [RNAn itch.io page](https://rnan.itch.io/) | CC0 | `Water/water_splash_01.wav`, `Water/water_splash_02.wav` |
| TinyWorlds | [Forest Ambience](https://opengameart.org/content/forest-ambience) | CC0 | `Ambience/forest_ambience.mp3` |
| JaggedStone | [Loopable Dungeon Ambience](https://opengameart.org/content/loopable-dungeon-ambience) | CC0 | `Ambience/prison_ambience.ogg` |
| qubodup | [Seamless City Loop](https://freesound.org/people/qubodup/sounds/223093/) | CC0 1.0 per the original Freesound page | `Ambience/city_ambience.mp3` (high-quality Freesound preview; original upload is FLAC) |
| Independent.nu, submitted by qubodup | [Heartbeat (single sound)](https://opengameart.org/content/heartbeat-single-sound) | CC0 | `Alerts/heartbeat.wav` (lossless PCM conversion from the source FLAC) |
| Lisboa | [Stealth Music](https://opengameart.org/content/stealth-music) | CC0 | `Music/stealth_woods.mp3` (`Stealth in the Woods.mp3`) |
| Adiutorium | [Infiltration](https://opengameart.org/content/infiltration) | CC0 | `Music/infiltration.mp3` |

The Peludo page requests that game users reference its itch.io page; the author's OpenGameArt profile links [RNAn on itch.io](https://rnan.itch.io/). Kenney also welcomes optional credit; it is not a condition of the listed CC0 license.

## Runtime event map

The exact asset paths are in `AudioManifest.json`. The manifest currently maps these event IDs:

| Event IDs | Intended clips |
| --- | --- |
| `player.step.prison`, `player.step.forest`, `player.step.wood`, `player.step.gravel`, `player.step.mud` | Concrete/stone, grass/leaves, wood, gravel, or mud variations |
| `player.step.water` | Two brief water splash clips; these are splash effects rather than a full repeated walking-footstep set |
| `interaction.door.open`, `interaction.door.close`, `interaction.switch`, `interaction.lock`, `hazard.bear_trap.snap`, `interaction.pickup`, `interaction.paper` | Door, switch, lock/latch, trap snap cue, pickup, and paper handling |
| `ui.click`, `ui.confirm`, `ui.cancel`, `ui.error`, `ui.panel.open`, `ui.checkpoint.saved` | Menu and feedback cues |
| `alert.guard`, `alert.heartbeat`, `animal.dog.bark` | Guard alarm, tension heartbeat, and dog bark |
| `throw.impact.soft`, `throw.impact.wood`, `throw.impact.metal`, `throw.impact.glass`, `throw.firecracker` | Throwable impacts, glass break, and a short firework pop |
| `ambience.prison`, `ambience.forest`, `ambience.city`, `music.stealth`, `music.chase` | Prison/cave, forest, real night-city recording, and stealth/chase music |

The metal latch/click files are available for a bear-trap snap cue; a dedicated bear-trap recording is not included. The city ambience is a 30-second seamless night-city ambience loop by qubodup, based on a CC0 field recording by jmbphilmes. Freesound exposes the high-quality MP3 preview without login; that preview is the imported file. Its source page identifies the recording as CC0 and gives the uploaded original as FLAC.

## Validation

`Tools/fetch_audio.py` downloads from the source records above and copies only curated source clips. The heartbeat is converted losslessly from FLAC to 16-bit PCM WAV (`ffmpeg -c:a pcm_s16le`) because Unity's standard audio importer does not support FLAC. Its original FLAC remains in the ignored download cache. On 2026-11-03, `ffprobe` successfully decoded all 42 imported audio clips and reported nonzero stream/format metadata. The clips have not been auditioned in this pass, so no claim is made about subjective fit or seamless runtime looping beyond the loop descriptions on their source pages.
