using System.Linq;
using BreakoutIsle.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BreakoutIsle.Tests
{
    public class PlayerInputAssetTests
    {
        private const string AssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        [Test]
        public void PlayerMapMatchesTheApprovedKeyboardAndMouseControls()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null, $"Missing input action asset at {AssetPath}.");
            var player = asset.FindActionMap("Player", throwIfNotFound: true);
            var expectedActions = new[] { "Move", "Sprint", "Sneak", "Interact", "Throw", "Pause", "Objectives", "SelectSlot1", "SelectSlot2", "SelectSlot3" };
            CollectionAssert.AreEquivalent(expectedActions, player.actions.Select(action => action.name));

            AssertBinding(player, "Move", "<Keyboard>/w");
            AssertBinding(player, "Move", "<Keyboard>/s");
            AssertBinding(player, "Move", "<Keyboard>/a");
            AssertBinding(player, "Move", "<Keyboard>/d");
            AssertBinding(player, "Sneak", "<Keyboard>/leftCtrl");
            AssertBinding(player, "Sprint", "<Keyboard>/leftShift");
            AssertBinding(player, "Interact", "<Keyboard>/e");
            AssertBinding(player, "Throw", "<Mouse>/leftButton");
            AssertBinding(player, "Pause", "<Keyboard>/escape");
            AssertBinding(player, "Objectives", "<Keyboard>/tab");
        }

        [Test]
        public void GeneratedRookIdleSheetUsesFullCellsFeetPivotAndPixelSettings()
        {
            const string sheetPath = "Assets/BreakoutIsle/Art/Characters/RookPrison/idle/idle.png";
            var importer = AssetImporter.GetAtPath(sheetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, $"Missing imported Rook sheet at {sheetPath}.");
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));

            var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToArray();
            Assert.That(sprites, Has.Length.EqualTo(32));
            var southIdle = sprites.Single(sprite => sprite.name == "idle_S_00");
            Assert.That(southIdle.rect.size, Is.EqualTo(new Vector2(64f, 64f)));
            Assert.That(southIdle.pivot.x, Is.EqualTo(32f).Within(0.001f));
            Assert.That(southIdle.pivot.y, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void PrisonTilesUseSinglePointFilteredSpritesAndAtlasStaysUnused()
        {
            const string tilePath = "Assets/BreakoutIsle/Art/Environment/Prison/Tiles/floor_clean.png";
            const string atlasPath = "Assets/BreakoutIsle/Art/Environment/Prison/Tiles/prison-tiles-atlas.png";
            var tile = AssetImporter.GetAtPath(tilePath) as TextureImporter;
            var atlas = AssetImporter.GetAtPath(atlasPath) as TextureImporter;

            Assert.That(tile, Is.Not.Null);
            Assert.That(tile.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(tile.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(tile.spritePixelsPerUnit, Is.EqualTo(32f));
            Assert.That(tile.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(tile.mipmapEnabled, Is.False);
            Assert.That(tile.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(atlas, Is.Not.Null);
            Assert.That(atlas.textureType, Is.EqualTo(TextureImporterType.Default));
        }

        [Test]
        public void PrisonFurnitureUsesTheNinetySixPixelCellAtThirtyTwoPpu()
        {
            const string bedPath = "Assets/BreakoutIsle/Art/Environment/Prison/Props/bed.png";
            const string ventPath = "Assets/BreakoutIsle/Art/Environment/Prison/Props/vent_00.png";
            var bed = AssetImporter.GetAtPath(bedPath) as TextureImporter;
            var vent = AssetDatabase.LoadAssetAtPath<Sprite>(ventPath);

            Assert.That(bed, Is.Not.Null);
            Assert.That(bed.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(bed.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(bed.spritePixelsPerUnit, Is.EqualTo(32f));
            Assert.That(bed.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(bed.mipmapEnabled, Is.False);
            Assert.That(bed.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(bedPath);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(96f, 96f)));
            Assert.That(sprite.pivot.y, Is.EqualTo(6f).Within(0.001f));
            Assert.That(vent, Is.Not.Null);
            Assert.That(vent.rect.size, Is.EqualTo(new Vector2(64f, 64f)));
            Assert.That(vent.pivot, Is.EqualTo(new Vector2(32f, 32f)));
        }

        [Test]
        public void InventoryIconsUseSinglePointFilteredThirtyTwoPixelSprites()
        {
            const string iconPath = "Assets/BreakoutIsle/Art/UI/Icons/spoon.png";
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(32f, 32f)));
        }

        [Test]
        public void UiSkinsUseTheAuthoredEightPixelNineSliceBorder()
        {
            const string panelPath = "Assets/BreakoutIsle/Art/UI/Skins/panel.png";
            var importer = AssetImporter.GetAtPath(panelPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteBorder, Is.EqualTo(new Vector4(8f, 8f, 8f, 8f)));
        }

        [Test]
        public void RookAnimatorUsesAllFourEightDirectionActionSets()
        {
            const string controllerPath = "Assets/BreakoutIsle/Animations/RookPrison/RookPrison.controller";
            const string clipsPath = "Assets/BreakoutIsle/Animations/RookPrison/Clips";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            Assert.That(controller, Is.Not.Null);
            CollectionAssert.AreEquivalent(new[] { "MoveMode", "FacingX", "FacingY" }, controller.parameters.Select(parameter => parameter.name));

            var directionTrees = AssetDatabase.LoadAllAssetsAtPath(controllerPath)
                .OfType<BlendTree>()
                .Where(tree => tree.name.EndsWith("8 Directions"))
                .ToArray();
            Assert.That(directionTrees, Has.Length.EqualTo(4));
            Assert.That(directionTrees.All(tree => tree.children.Length == 8), Is.True);

            var clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { clipsPath });
            Assert.That(clipGuids, Has.Length.EqualTo(32));
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BreakoutIsle/Prefabs/Characters/RookPlayer.prefab");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(playerPrefab.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
            Assert.That(playerPrefab.GetComponent<Animator>().runtimeAnimatorController, Is.EqualTo(controller));
            Assert.That(playerPrefab.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(playerPrefab.GetComponent<PlayerMotor>(), Is.Not.Null);
            Assert.That(playerPrefab.GetComponent<PlayerInputReader>(), Is.Not.Null);
            Assert.That(playerPrefab.GetComponent<YSortRenderer>(), Is.Not.Null);
        }

        private static void AssertBinding(InputActionMap map, string action, string path)
        {
            Assert.That(map.FindAction(action).bindings.Any(binding => binding.path == path), Is.True, $"{action} is missing {path}.");
        }
    }
}
