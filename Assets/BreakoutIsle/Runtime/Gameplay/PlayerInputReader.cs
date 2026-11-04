using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BreakoutIsle.Gameplay
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";

        private InputActionMap _map;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _sneak;
        private InputAction _interact;
        private InputAction _throw;
        private InputAction _pause;
        private InputAction _objectives;
        private InputAction[] _slots;
        private Action<InputAction.CallbackContext>[] _slotCallbacks;
        private bool _throwStartedOverUi;
        private bool _gameplayBlocked;
        private bool _ignoreGameplayUntilNeutral;

        private bool GameplayInputBlocked => _gameplayBlocked || _ignoreGameplayUntilNeutral;
        public Vector2 Move => GameplayInputBlocked || _move == null ? Vector2.zero : _move.ReadValue<Vector2>();
        public bool SprintHeld => !GameplayInputBlocked && _sprint != null && _sprint.IsPressed();
        public bool SneakHeld => !GameplayInputBlocked && _sneak != null && _sneak.IsPressed();

        public event Action InteractPressed;
        public event Action ThrowPressed;
        public event Action ThrowReleased;
        public event Action PausePressed;
        public event Action ObjectivesPressed;
        public event Action<int> SlotSelected;

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("PlayerInputReader needs the project's InputActionAsset assigned.", this);
                enabled = false;
                return;
            }

            _map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            _move = _map.FindAction("Move", throwIfNotFound: true);
            _sprint = _map.FindAction("Sprint", throwIfNotFound: true);
            _sneak = _map.FindAction("Sneak", throwIfNotFound: true);
            _interact = _map.FindAction("Interact", throwIfNotFound: true);
            _throw = _map.FindAction("Throw", throwIfNotFound: true);
            _pause = _map.FindAction("Pause", throwIfNotFound: true);
            _objectives = _map.FindAction("Objectives", throwIfNotFound: true);
            _slots = new[]
            {
                _map.FindAction("SelectSlot1", throwIfNotFound: true),
                _map.FindAction("SelectSlot2", throwIfNotFound: true),
                _map.FindAction("SelectSlot3", throwIfNotFound: true)
            };
            _slotCallbacks = new Action<InputAction.CallbackContext>[_slots.Length];
            for (var i = 0; i < _slotCallbacks.Length; i++)
            {
                var selectedSlot = i + 1;
                _slotCallbacks[i] = _ => { if (!GameplayInputBlocked) SlotSelected?.Invoke(selectedSlot); };
            }
        }

        private void OnEnable()
        {
            if (_map == null)
                return;

            _interact.performed += OnInteract;
            _throw.started += OnThrowStarted;
            _throw.canceled += OnThrowCanceled;
            _pause.performed += OnPause;
            _objectives.performed += OnObjectives;
            for (var i = 0; i < _slots.Length; i++)
                _slots[i].performed += _slotCallbacks[i];
            _map.Enable();
        }

        private void Update()
        {
            if (_ignoreGameplayUntilNeutral && GameplayControlsAreReleased())
                _ignoreGameplayUntilNeutral = false;
        }

        private void OnDisable()
        {
            if (_map == null)
                return;

            _interact.performed -= OnInteract;
            _throw.started -= OnThrowStarted;
            _throw.canceled -= OnThrowCanceled;
            _pause.performed -= OnPause;
            _objectives.performed -= OnObjectives;
            for (var i = 0; i < _slots.Length; i++)
                _slots[i].performed -= _slotCallbacks[i];
            _map.Disable();
        }

        public void SetGameplayBlocked(bool blocked) => _gameplayBlocked = blocked;
        public void ResetGameplayInputUntilNeutral()
        {
            _ignoreGameplayUntilNeutral = true;
            _throwStartedOverUi = true;
        }

        private void OnInteract(InputAction.CallbackContext _) { if (!GameplayInputBlocked) InteractPressed?.Invoke(); }
        private void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
        private void OnObjectives(InputAction.CallbackContext _) => ObjectivesPressed?.Invoke();

        private void OnThrowStarted(InputAction.CallbackContext _)
        {
            _throwStartedOverUi = GameplayInputBlocked || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            if (!_throwStartedOverUi)
                ThrowPressed?.Invoke();
        }

        private void OnThrowCanceled(InputAction.CallbackContext _)
        {
            if (!_throwStartedOverUi && !GameplayInputBlocked)
                ThrowReleased?.Invoke();
            _throwStartedOverUi = false;
        }

        private bool GameplayControlsAreReleased()
        {
            if (_move == null || _sprint == null || _sneak == null || _interact == null || _throw == null || _slots == null)
                return true;
            if (_move.ReadValue<Vector2>().sqrMagnitude > 0.001f || _sprint.IsPressed() || _sneak.IsPressed() ||
                _interact.IsPressed() || _throw.IsPressed()) return false;
            foreach (var slot in _slots)
                if (slot.IsPressed()) return false;
            return true;
        }
    }
}
