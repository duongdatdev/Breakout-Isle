using System.Linq;
using NUnit.Framework;
using UnityEditor;
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

        private static void AssertBinding(InputActionMap map, string action, string path)
        {
            Assert.That(map.FindAction(action).bindings.Any(binding => binding.path == path), Is.True, $"{action} is missing {path}.");
        }
    }
}
