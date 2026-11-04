using BreakoutIsle.Gameplay;
using UnityEngine;

namespace BreakoutIsle.Audio
{
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerInteractionAudio : MonoBehaviour
    {
        private PlayerInputReader _input;
        private PlayerInteractor _interactor;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _interactor = GetComponent<PlayerInteractor>();
        }

        private void OnEnable() { if (_input != null) _input.InteractPressed += OnInteract; }
        private void OnDisable() { if (_input != null) _input.InteractPressed -= OnInteract; }

        private void OnInteract()
        {
            if (_interactor == null || _interactor.Current == null || AudioService.Instance == null) return;
            var target = _interactor.Current;
            if (target is QuestPickup) AudioService.Instance.Play("interaction.pickup");
            else if (target is QuestActionInteractable) AudioService.Instance.Play("interaction.door.open");
            else AudioService.Instance.Play("interaction.lock");
        }
    }
}
