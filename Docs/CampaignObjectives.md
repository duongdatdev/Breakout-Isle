# Campaign objective data plan

This document maps the ten level chains in `GameDesignOriginal.txt` onto the current `LevelObjectiveSet` / `ObjectiveStepDefinition` model. It is an authoring plan; it does not claim that all ten ScriptableObject assets or scenes exist yet.

Each level uses its own objective IDs. Quest item IDs are also level-scoped unless listed under **Campaign carry**. `requiredCompletedObjectiveIds` expresses an all-of gate, `canCompleteOutOfOrder` is for independent pickups or switches, and `sequenceGroupId` / `sequenceIndex` is reserved for the ordered sewer valves. Item gates are placed on the action that consumes the item.

## Level chains

| Level | Objective ID | Completion / gate | Result |
| --- | --- | --- | --- |
| 1 — Cell block | `spoon_found` | Pick up `spoon` | — |
|  | `vent_opened` | Vent action requires and consumes `spoon` | — |
|  | `card_found` | Pick up `guard_keycard`; requires `vent_opened` | — |
|  | `cellblock_exit_opened` | Exit action requires and consumes `guard_keycard`; requires `card_found` | Level 1 complete; checkpoint after vent and after card as authored |
| 2 — Canteen and laundry | `power_cut` | Operate engineering lever | Lights switch off, exposing chest |
|  | `laundry_chest_key_found` | Pick up `laundry_chest_key`; requires `power_cut` | — |
|  | `yard_gate_key_found` | Chest action requires and consumes `laundry_chest_key`; rewards `yard_gate_key` | Checkpoint after chest |
|  | `yard_gate_opened` | Exit action requires and consumes `yard_gate_key` | Level 2 complete |
| 3 — Yard and towers | `wirecutters_found` | Pick up `wirecutters` | — |
|  | `tower_power_a_off` | Operate first switch; `canCompleteOutOfOrder` | — |
|  | `tower_power_b_off` | Operate second switch; `canCompleteOutOfOrder` | All-of gate below |
|  | `fence_cut` | Fence action requires and consumes `wirecutters`, and requires both tower switch IDs | — |
|  | `wall_climbed` | Climb action requires `fence_cut` | Level 3 complete |
| 4 — Forest edge | `map_piece_a_found`, `map_piece_b_found`, `map_piece_c_found` | Three separate pickups; all `canCompleteOutOfOrder` | — |
|  | `burial_site_revealed` | Requires all three map piece IDs | — |
|  | `shovel_found` | Pick up `shovel`; requires `burial_site_revealed` | — |
|  | `buried_chest_dug` | Dig action requires and consumes `shovel` | Chest becomes available |
|  | `bridge_key_found` | Chest action rewards `bridge_key` | Checkpoint after map assembly and chest |
|  | `bridge_exit_opened` | Exit action requires and consumes `bridge_key` | Level 4 complete |
| 5 — Swamp | `dam_gate_lowered` | Operate dam lever | Path and chest become available |
|  | `watchtower_chest_key_found` | Pick up `swamp_chest_key`; requires `dam_gate_lowered` | — |
|  | `skiff_key_found` | Chest action requires and consumes `swamp_chest_key`; rewards `skiff_key` | Checkpoint after chest |
|  | `skiff_exit_opened` | Exit action requires and consumes `skiff_key` | Level 5 complete |
| 6 — Ranger station | `electric_fence_disabled` | Operate breaker | — |
|  | `station_gate_key_found` | Pick up `station_gate_key`; requires `electric_fence_disabled` | — |
|  | `station_gate_opened` | Gate action requires and consumes `station_gate_key` | — |
|  | `truck_escape_started` | Truck action requires `station_gate_opened` | Level 6 complete |
| 7 — Shanty town | `civilian_clothes_found` | Pick up and equip `civilian_outfit` | Outfit state is persisted |
|  | `pawnshop_owner_distracted` | Distract action | Store safe becomes accessible |
|  | `pawnshop_safe_key_found` | Pick up `pawnshop_safe_key`; requires `pawnshop_owner_distracted` | — |
|  | `cipher_note_found` | Safe action requires and consumes `pawnshop_safe_key`; rewards campaign item `cipher_note` | Checkpoint after note |
|  | `city_transition_opened` | Level exit requires `cipher_note` | Level 7 complete; preserve cipher for level 8 |
| 8 — Sewer | `valve_a_opened`, `valve_b_opened`, `valve_c_opened` | Same `sequenceGroupId` with indices 0, 1, 2; wrong action resets this group | Barrier opens after all three |
|  | `maintenance_chest_key_found` | Pick up `sewer_chest_key`; requires all three valve IDs and campaign item `cipher_note` | — |
|  | `market_exit_key_found` | Chest action requires and consumes `sewer_chest_key`; rewards `market_exit_key` | Checkpoint after valves and chest |
|  | `sewer_exit_opened` | Exit action requires and consumes `market_exit_key` | Level 8 complete |
| 9 — Night market and police station | `market_cameras_disabled` | Operate camera power switch | — |
|  | `harbor_pass_found` | Pick up `harbor_pass`; requires `market_cameras_disabled` | Checkpoint after pass; preserve pass for level 10 |
|  | `harbor_gate_opened` | Exit action requires `harbor_pass` | Level 9 complete |
| 10 — Harbor | `courier_package_found` | Find the crew's stored package | — |
|  | `container_route_opened` | Crane action requires `courier_package_found` | Container route opens |
|  | `boat_key_found` | Package action requires `container_route_opened` and campaign item `harbor_pass`; rewards `boat_key` | Trigger global alarm and 60-second finale timer; checkpoint after key |
|  | `boat_escape_started` | Boat action requires and consumes `boat_key` before timer expiry | Campaign complete |

## Campaign carry

- Preserve the three support slots and selected slot across every level transition.
- Preserve outfit across transitions, including the civilian outfit acquired in level 7.
- Preserve explicitly authored persistent story flags.
- Preserve only the campaign quest items `cipher_note` from level 7 to level 8 and `harbor_pass` from level 9 to level 10. Other quest items remain level-local and clear on transition.
- Build the destination save from that scene's authored world baseline and checkpoint anchor. Do not copy source-level world entity records into a destination snapshot.
- Keep the source profile as primary until the destination scene, session, world baseline, checkpoint anchor, and initial snapshot are all valid. Failed scene loading or initialization must leave the source profile recoverable.

## Authoring checks

- Use unique nonempty objective and world entity IDs within each level.
- Make all independent pickups/switches `canCompleteOutOfOrder`; gate the following objective on every required ID.
- Give the three sewer valves one group ID and exact indices 0–2. Test wrong-order reset before authoring the sewer scene.
- Make an item requirement and consumption explicit on the gate/chest/action, not on the preceding pickup. Chest rewards belong on the chest objective.
- Assign checkpoint IDs to authored safe anchors and request saves only after the corresponding world action has committed.
- Add level data assets and scene bindings only after the current Level 1 save/quest integration passes.
