# Breakout Isle — approved implementation contract

Approved 2026-11-03. Implementer: gpt-6-luna. Root agent coordinates art, audio, reviews and Unity verification. Continue automatically after a passed checkpoint. Stop immediately if Unity MCP is unavailable; do not substitute an unverified scene or claim a checkpoint passed.

## Product

Single-player PC 2D top-down stealth/puzzle escape game, Vietnamese UI. Complete level 1 with production pixel art, then build all ten levels across prison, forest and port city. Windows is the verified build target; macOS compatibility is maintained but requires an actual Mac for runtime verification. First playthrough target 60–100 minutes, 90–120 with exploration/retries.

Read GameDesignOriginal.txt for the full quest chains and all original parameters. This contract resolves its ambiguities. The live project is now the Unity 6000.6.6f1 **2D template**, changed since planning; reuse its Renderer2D and installed packages rather than converting the previous 3D template. Preserve ALL user changes present before implementation. Never stage unrelated existing changes in manifest, packages-lock, ProjectSettings.asset, learning settings or welcome image.

## Gameplay rules

- WASD normalized eight-direction movement. Sneak Ctrl 1.5 m/s; walk 3; sprint Shift 6. Stamina 100, moving sprint drains 20/s; recovery 15/s after 1 second without sprint. Exhaustion falls back to walking. Esc pause; Tab map/objectives pauses gameplay. E nearest eligible interactable, 1–3 selects support slot; hold/release left click throws a throwable, click consumes energy drink. UI clicks must not trigger world actions.
- Three support slots, one item per slot, full inventory refuses pickup with feedback; quest items tracked separately. Default throw range 6 m, 0.6 s flight with visible arc and landing shadow; walls obstruct the throw. Stones/cans noise 6 m and attract nearest available guard; bottle 10 m multiple guards; firecracker 3 s fuse, 12 m noise and draws city crowds; meat distracts dogs 8 s; energy fills stamina and grants free sprint 5 s.
- Guards patrol 2.5 m/s, chase 5.5; vision 90 degrees, 6 m, blocked by obstacles. Detection at 6 m takes 1.5 s; interpolate to 0.5 s at 1 m. Sneak multiplier .7 and dark-zone multiplier .3 multiply together. Remove original contradictory hard 1.5 m dark visibility cutoff. Hidden is zero visual detection unless entry was witnessed; dogs can smell through bushes.
- Guard states Patrol, Suspicious (50% detection or sound), Chase (100%; notify guards within 10 m once per alert), Search (after 3 s lost sight, 10 s near last known location), Alert (patrol with +20% range for 30 s). Detection decreases to zero over 2 s without sight. Guards never track live player position through walls. Witnessed hiding entry causes inspection of that exact place; otherwise search checks at most one nearby hiding place. Capture only by a chasing guard touching an exposed player, or inspection of an occupied hiding place.
- Cameras sweep 60 degrees and notify nearest guard; switch disables linked cameras. Tower spotlight immediately detects on unobstructed illumination. Bear trap revealed within 2 m, roots for 3 s and emits 10 m sound. Tripwire 15 m alarm; sneaking bypasses safely.
- Dogs speed 7 m/s, scent radius 5 m; follow time-limited scent breadcrumbs, not omniscient player coordinates. Water clears scent. Dogs bark to alert guards. Civilians vision 120 degrees/5 m with line of sight, report suspicious action/prison outfit after continuous 2 s and notify guards within 15 m; do not capture. Normal clothing suppresses outfit suspicion, not sprint/throw suspicion. Crowd clusters reduce visual detection like dark zones; do not form impassable collision walls.

## Checkpoint/save contract

- Initial save and 1–2 quest checkpoints per level. Quest milestone requests save; defer during chase and save once chase ends. Separate pending/saved HUD feedback. Spawn is an authored safe anchor, not arbitrary player position.
- Versioned JSON single local profile in Application.persistentDataPath. Snapshot includes level/checkpoint IDs, completed quests, persistent story flags, quest items, three support slots, outfit, per-entity door/switch/chest/trap/pickup states. Use stable serialized IDs, never instance IDs. Atomic write via temporary file and backup; recover backup on corrupt primary, otherwise offer explicit restart without silently erasing data.
- Restore the entire snapshot including respawned post-checkpoint pickups and restored inventory quantities; no duplication. Full stamina, clear transient projectiles/noises/buffs; reset guards to scene baseline modified by saved scripted flags.
- Carry support items, outfit and relevant cross-level quest flags; preserve level 7 cipher into level 8 and level 9 port pass into level 10. Other level-local quest items clear on transition.
- Finale: save after boat key, restore global alert and a fresh 60 s timer on retry/Continue. Reaching boat wins; capture or timer expiry retries.

## Architecture and asset contract

- Reuse Unity 2D URP, Input System, Physics2D, Tilemap, Animator, uGUI and installed Test Framework. Bootstrap/menu scene and ten level scenes. Shared prefabs. ScriptableObject level/item/tuning definitions; small interfaces for interaction, hearing and checkpoint participants. A* 2D navigation grid updated by blockers. No unnecessary framework/dependency.
- Pixel art: 32 px tiles, 32 PPU, 64 px character cells, feet pivot, Point filtering, no mipmaps, integer-scale 640x360 reference, 1280x720/1920x1080 QA. Eight distinct directional sprite sets, no mirroring to replace missing directions. Same palette and identity throughout.
- Use generate2dmap and generate2dsprite skills with built-in image_gen for ALL visible raw artwork. Scripts may only clean magenta, extract, align, resize deterministically, QC and assemble. Runtime collision/debug/vision cones/noise rings/throw arcs/progress fills may be geometric. No final primitive player/environment stand-ins.
- Separate foundation tiles from runtime doors/chests/trees/props/hazards/pickups/actors. Tile collision and scene metadata define gameplay, not a baked map picture. Tall props use y sorting and fade when occluding the player.
- Rook both prison and civilian outfit: eight-direction idle 4 frames, walk/run/sneak 6, interact/throw/hide entry/exit 4–6, caught 6. Mission-specific crawl, pry/cut, dig, valve and climb use actual authored scene orientations. Guards eight-direction idle/walk/run/search/capture; flashlight variant. Dogs eight-direction idle/walk/run/sniff/bark/eat. At least three civilian appearances using shared appropriate eight-direction motion.
- Doors/chests open and closed states plus transition, switches/valves, camera sweep, trap activation, water, crane/container/boat; separate impact/glass/firecracker/pickup effects. Idle 8 FPS, walk/sneak 10, run 12, actions 10 FPS unless specific timing requires change.
- Per-action/direction grids, consistent identity/scale/feet, no cropped parts or magenta. Produce transparent sheets, preview GIFs, prompts and QC manifest. QC before atlas packing. Missing clips cannot silently fall back to unrelated animation for acceptance.
- UI: Main Menu/New Game/Continue/Settings/Pause, stamina and three slots, quest checklist, minimap, Tab map/objectives, caught/retry, ending, save status. Generated pixel-art icons/frame/button skins; real text with Vietnamese glyphs. UI layout responds at supported resolutions. Keyboard hints and explicit sound/text for detection.
- User explicitly requires downloaded internet audio, not generated audio. Find and verify license on original source pages; prefer CC0. Keep source URL/author/license plus included license files and sound mapping in Docs/AudioSources.md. No paid assets. Steps/surfaces, interactions, pickup/save/UI, guard alarm, dog, traps, glass/firecracker, water/ambient and stealth/chase music. Wire clips to gameplay; a folder of unused audio is incomplete.

## Delivery checkpoints

Each checkpoint has small independent commits; passing requires code, relevant final assets, runtime evidence and tests. Track actual status in Progress.md, never mark incomplete art passed.

| ID | Delivery | Save anchors / acceptance |
|---|---|---|
| CP0 | Baseline, docs, 2D settings/input/layers/test harness, MCP | Correct project, clean compile, 2D scene boots; preserve existing dirty files. |
| CP1 | Prison art baseline, Rook eight directions/core actions, movement/camera/stamina | In-engine visuals and animation previews, normalized movement and stable collisions. |
| CP2 | Playable level 1 spoon/vent/card/exit, stealth/guards/hiding/capture, snapshot/Continue, minimal HUD | After vent and card; retry/relaunch each save without softlock. |
| CP3 | Finished level 1 art, mission animations, complete menu/HUD/audio/tutorial/transition | Production representative, no temporary art/missing clips. |
| CP4 | Level 2 laundry/cafeteria, inventory/throw, cameras/power/chest | After power and chest; consistent restore of all world state. |
| CP5 | Level 3 yard/towers, two switches, fence cut/climb, guard groups | After both lights and fence; continuous prison playthrough. |
| CP6 | Level 4 forest, 3 map pieces, shovel/dig/chest, bear trap | After assembled map and dug chest; readable trap cues. |
| CP7 | Levels 5–6 swamp/ranger station, dog/meat/scent/water/mud/dam/electric fence/tripwire/truck | L5 after water lowered/chest; L6 power/key; escape dog both ways. |
| CP8 | Levels 7–8 city/sewer, clothing/civilians/safe/cipher/valves | L7 clothing/cipher; L8 valves/chest; persist outfit/cipher. |
| CP9 | Levels 9–10 market/police station/port, crowd/firecracker/energy, camera shutdown/pass/crane/boat/finale | L9 power/pass; L10 container/boat key; win and retry countdown. |
| CP10 | Full campaign balance, asset/UI/audio/save audit, optimization, Windows build | All ten levels playable without softlock; build outside Editor, report measured performance. |

## Working and verification rules

- Before Unity tools, check mcpforunity://editor/state and correct instance. Inspect APIs with unity_reflect and actual installed source before using uncertain Unity APIs. After changes await compilation and check console; save scene/assets. Avoid simultaneous Editor mutation by multiple agents.
- Small logical Conventional Commits: feat(scope):, fix(scope):, test:, docs:, refactor:, chore:. Include related .meta/Animator/prefab together. Stage explicit paths/hunks, never git add .; do not stage pre-existing user changes. Do not push. Never commit compile errors or dangling references.
- EditMode tests: normalized input/stamina, objectives/valves, snapshot/pickup integrity/corrupt-save handling. PlayMode: line of sight/state transitions/noise/hiding/dogs/interactions/scene changes. Save acceptance: before/after each checkpoint, consume/pickup/retry, quit/relaunch, finale timer. Visual QA every direction/action and actual Unity camera, sorting, 720p/1080p HUD. Test a valid complete route in every level and continuous campaign. Target 60 FPS at 1080p; report actual hardware and measurements.
- Every checkpoint report includes completed tasks, tests, visual evidence, commit IDs, remaining work and next checkpoint. Continue automatically after pass. MCP loss stops all implementation immediately per user request.
