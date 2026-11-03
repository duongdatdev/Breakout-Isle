using System.Linq;
using NUnit.Framework;
using UnityEditor;
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

        private static void AssertBinding(InputActionMap map, string action, string path)
        {
            Assert.That(map.FindAction(action).bindings.Any(binding => binding.path == path), Is.True, $"{action} is missing {path}.");
        }
    }
}
