#!/usr/bin/env python3
"""Generate the authored LevelObjectiveSet assets for campaign levels 2–10."""

from pathlib import Path
from uuid import UUID, uuid5


ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "Assets" / "BreakoutIsle" / "Data"
SCRIPT_GUID = "832e96b4477c5cb4e9dc17736a4af1a5"
CAMPAIGN_SCRIPT_GUID = "8da14800a8d64b9db9c3b495376bd7f7"
GUID_NAMESPACE = UUID("a8713251-8b96-48a6-97b7-e8ea5ca0c837")


def step(objective_id, description, *, prerequisite="", requires=(), out_of_order=False,
         sequence_group="", sequence_index=0, item="", consume=False, reward=""):
    return {
        "id": objective_id,
        "description": description,
        "prerequisiteId": prerequisite,
        "requiredCompletedObjectiveIds": list(requires),
        "canCompleteOutOfOrder": out_of_order,
        "sequenceGroupId": sequence_group,
        "sequenceIndex": sequence_index,
        "requiredQuestItemId": item,
        "consumeRequiredItem": consume,
        "rewardQuestItemId": reward,
    }


LEVELS = [
    ("Level02_Objectives", "prison-02", [
        step("power_cut", "Cut power in the engineering room"),
        step("laundry_chest_key_found", "Find the laundry chest key", prerequisite="power_cut", reward="laundry_chest_key"),
        step("yard_gate_key_found", "Open the chest and take the yard gate key", prerequisite="laundry_chest_key_found", item="laundry_chest_key", consume=True, reward="yard_gate_key"),
        step("yard_gate_opened", "Open the yard gate", prerequisite="yard_gate_key_found", item="yard_gate_key", consume=True),
    ]),
    ("Level03_Objectives", "prison-03", [
        step("wirecutters_found", "Find the wire cutters", reward="wire_cutters"),
        step("tower_power_a_off", "Disable the first tower light", out_of_order=True),
        step("tower_power_b_off", "Disable the second tower light", out_of_order=True),
        step("fence_cut", "Cut through the fence", requires=("tower_power_a_off", "tower_power_b_off"), item="wire_cutters", consume=True),
        step("wall_climbed", "Climb out beyond the fence", prerequisite="fence_cut"),
    ]),
    ("Level04_Objectives", "forest-04", [
        step("map_piece_a_found", "Find map fragment A", out_of_order=True, reward="map_fragment_a"),
        step("map_piece_b_found", "Find map fragment B", out_of_order=True, reward="map_fragment_b"),
        step("map_piece_c_found", "Find map fragment C", out_of_order=True, reward="map_fragment_c"),
        step("burial_site_revealed", "Assemble the map and locate the buried chest", requires=("map_piece_a_found", "map_piece_b_found", "map_piece_c_found")),
        step("shovel_found", "Find the shovel at the ranger tent", prerequisite="burial_site_revealed", reward="shovel"),
        step("buried_chest_dug", "Dig up the buried chest", prerequisite="shovel_found", item="shovel", consume=True),
        step("bridge_key_found", "Open the chest and take the bridge key", prerequisite="buried_chest_dug", reward="bridge_key"),
        step("bridge_exit_opened", "Unlock the bridge exit", prerequisite="bridge_key_found", item="bridge_key", consume=True),
    ]),
    ("Level05_Objectives", "forest-05", [
        step("dam_gate_lowered", "Lower the swamp water at the dam lever"),
        step("watchtower_chest_key_found", "Find the watchtower chest key", prerequisite="dam_gate_lowered", reward="swamp_chest_key"),
        step("skiff_key_found", "Open the chest and take the skiff key", prerequisite="watchtower_chest_key_found", item="swamp_chest_key", consume=True, reward="skiff_key"),
        step("skiff_exit_opened", "Unlock the skiff exit", prerequisite="skiff_key_found", item="skiff_key", consume=True),
    ]),
    ("Level06_Objectives", "forest-06", [
        step("electric_fence_disabled", "Switch off the electric fence"),
        step("station_gate_key_found", "Find the ranger station gate key", prerequisite="electric_fence_disabled", reward="station_gate_key"),
        step("station_gate_opened", "Unlock the station gate", prerequisite="station_gate_key_found", item="station_gate_key", consume=True),
        step("truck_escape_started", "Hide in the cargo truck", prerequisite="station_gate_opened"),
    ]),
    ("Level07_Objectives", "city-07", [
        step("civilian_clothes_found", "Take civilian clothes from the washing line", reward="civilian_clothes"),
        step("pawnshop_owner_distracted", "Distract the pawnshop owner", prerequisite="civilian_clothes_found"),
        step("pawnshop_safe_key_found", "Find the pawnshop safe key", prerequisite="pawnshop_owner_distracted", reward="pawnshop_safe_key"),
        step("cipher_note_found", "Open the safe and take the cipher note", prerequisite="pawnshop_safe_key_found", item="pawnshop_safe_key", consume=True, reward="cipher_note"),
        step("city_transition_opened", "Use the cipher note to find the sewer entrance", prerequisite="cipher_note_found", item="cipher_note"),
    ]),
    ("Level08_Objectives", "city-08", [
        step("valve_a_opened", "Turn the first valve", sequence_group="sewer-valves", sequence_index=0, item="cipher_note"),
        step("valve_b_opened", "Turn the second valve", sequence_group="sewer-valves", sequence_index=1),
        step("valve_c_opened", "Turn the third valve", sequence_group="sewer-valves", sequence_index=2),
        step("maintenance_chest_key_found", "Find the maintenance chest key", requires=("valve_a_opened", "valve_b_opened", "valve_c_opened"), item="cipher_note", reward="sewer_chest_key"),
        step("market_exit_key_found", "Open the chest and take the market exit key", prerequisite="maintenance_chest_key_found", item="sewer_chest_key", consume=True, reward="market_exit_key"),
        step("sewer_exit_opened", "Unlock the sewer exit", prerequisite="market_exit_key_found", item="market_exit_key", consume=True),
    ]),
    ("Level09_Objectives", "city-09", [
        step("market_cameras_disabled", "Disable the night market cameras"),
        step("port_pass_found", "Find the harbor access pass", prerequisite="market_cameras_disabled", reward="port_pass"),
        step("harbor_gate_opened", "Use the harbor pass to exit the market", prerequisite="port_pass_found", item="port_pass"),
    ]),
    ("Level10_Objectives", "city-10", [
        step("harbor_access_opened", "Use the harbor pass to enter the port", item="port_pass", consume=True),
        step("courier_package_found", "Find the crew's stored package", prerequisite="harbor_access_opened", reward="courier_package"),
        step("container_route_opened", "Operate the crane to open the container route", prerequisite="courier_package_found", item="courier_package"),
        step("boat_key_found", "Open the package and take the boat key", prerequisite="container_route_opened", item="courier_package", consume=True, reward="boat_key"),
        step("boat_escape_started", "Unlock the boat and escape", prerequisite="boat_key_found", item="boat_key", consume=True),
    ]),
]

CAMPAIGN_LEVELS = [
    ("prison-01", "Level01_Prison", "cell-start", ()),
    ("prison-02", "Level02_DiningLaundry", "prison-02-start", ()),
    ("prison-03", "Level03_PrisonYard", "prison-03-start", ()),
    ("forest-04", "Level04_ForestEdge", "forest-04-start", ()),
    ("forest-05", "Level05_Swamp", "forest-05-start", ()),
    ("forest-06", "Level06_RangerStation", "forest-06-start", ()),
    ("city-07", "Level07_ShantyTown", "city-07-start", ("cipher_note",)),
    ("city-08", "Level08_Sewer", "city-08-start", ()),
    ("city-09", "Level09_NightMarket", "city-09-start", ("port_pass",)),
    ("city-10", "Level10_Harbor", "city-10-start", ()),
]


def yaml_quote(value):
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"' if value else '""'


def render_asset(name, level_id, steps):
    lines = [
        "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
        "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}", f"  m_Name: {name}",
        "  m_EditorClassIdentifier: BreakoutIsle.Runtime::BreakoutIsle.Gameplay.LevelObjectiveSet",
        f"  levelId: {level_id}", "  steps:",
    ]
    for item in steps:
        lines.extend([
            f"  - id: {item['id']}", f"    description: {yaml_quote(item['description'])}",
            f"    prerequisiteId: {yaml_quote(item['prerequisiteId'])}",
            "    requiredCompletedObjectiveIds: " + ("[" + ", ".join(item["requiredCompletedObjectiveIds"]) + "]" if item["requiredCompletedObjectiveIds"] else "[]"),
            f"    canCompleteOutOfOrder: {1 if item['canCompleteOutOfOrder'] else 0}",
            f"    sequenceGroupId: {yaml_quote(item['sequenceGroupId'])}",
            f"    sequenceIndex: {item['sequenceIndex']}",
            f"    requiredQuestItemId: {yaml_quote(item['requiredQuestItemId'])}",
            f"    consumeRequiredItem: {1 if item['consumeRequiredItem'] else 0}",
            f"    rewardQuestItemId: {yaml_quote(item['rewardQuestItemId'])}",
        ])
    return "\n".join(lines) + "\n"


def campaign_asset_guid(index):
    return uuid5(GUID_NAMESPACE, f"Assets/BreakoutIsle/Data/CampaignLevel{index:02d}.asset").hex


def render_campaign_asset(index, level):
    level_id, scene_name, checkpoint_id, carried_items = level
    next_reference = "{fileID: 0}" if index == len(CAMPAIGN_LEVELS) else (
        f"{{fileID: 11400000, guid: {campaign_asset_guid(index + 1)}, type: 2}}"
    )
    items = "[]" if not carried_items else "\n".join(f"  - {item}" for item in carried_items)
    lines = [
        "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
        "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {CAMPAIGN_SCRIPT_GUID}, type: 3}}",
        f"  m_Name: CampaignLevel{index:02d}",
        "  m_EditorClassIdentifier: BreakoutIsle.Runtime::BreakoutIsle.Gameplay.CampaignLevelDefinition",
        f"  levelId: {level_id}", f"  sceneName: {scene_name}", f"  initialCheckpointId: {checkpoint_id}",
        f"  nextLevel: {next_reference}", f"  isFinalLevel: {1 if index == len(CAMPAIGN_LEVELS) else 0}",
        "  carryQuestItemIdsToNextLevel: " + items,
    ]
    return "\n".join(lines) + "\n"


def main():
    DATA.mkdir(parents=True, exist_ok=True)
    for name, level_id, steps in LEVELS:
        asset_path = DATA / f"{name}.asset"
        asset_path.write_text(render_asset(name, level_id, steps), encoding="utf-8", newline="\n")
        asset_guid = uuid5(GUID_NAMESPACE, str(asset_path.relative_to(ROOT)).replace("\\", "/")).hex
        meta_path = Path(str(asset_path) + ".meta")
        meta_path.write_text(
            f"fileFormatVersion: 2\nguid: {asset_guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
            encoding="utf-8", newline="\n",
        )
    for index, level in enumerate(CAMPAIGN_LEVELS, 1):
        asset_path = DATA / f"CampaignLevel{index:02d}.asset"
        asset_path.write_text(render_campaign_asset(index, level), encoding="utf-8", newline="\n")
        asset_guid = campaign_asset_guid(index)
        meta_path = Path(str(asset_path) + ".meta")
        meta_path.write_text(
            f"fileFormatVersion: 2\nguid: {asset_guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
            encoding="utf-8", newline="\n",
        )


if __name__ == "__main__":
    main()
