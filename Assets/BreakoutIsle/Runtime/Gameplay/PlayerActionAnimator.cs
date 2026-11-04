using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerActionAnimator : MonoBehaviour
    {
        [Serializable]
        public struct ActionDefinition
        {
            public string action;
            public int frameCount;
            public int fps;
            public bool loop;
        }

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private LevelSession session;
        [SerializeField] private PlayerStealthState stealth;
        [SerializeField] private SupportItemController supportItems;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private ActionDefinition[] actions = Array.Empty<ActionDefinition>();

        private readonly Dictionary<string, ActionDefinition> _byName = new Dictionary<string, ActionDefinition>(StringComparer.Ordinal);
        private Coroutine _completion;
        private bool _previousGameplayBlocked;
        private bool _ownsGameplayBlock;
        private int _gameplayBlockRevision;
        private bool _subscribed;

        public bool IsPlayingAction { get; private set; }
        public string CurrentAction { get; private set; }
        public event Action<string> ActionCompleted;

        public void Configure(Animator targetAnimator, PlayerMotor playerMotor, PlayerInputReader playerInput, ActionDefinition[] definitions)
        {
            UnsubscribeGameplayEvents();
            animator = targetAnimator;
            motor = playerMotor;
            input = playerInput;
            spriteRenderer = targetAnimator != null ? targetAnimator.GetComponent<SpriteRenderer>() : null;
            actions = definitions ?? Array.Empty<ActionDefinition>();
            RebuildLookup();
            SubscribeGameplayEvents();
        }

        public void BindGameplayEvents(PlayerInteractor playerInteractor, LevelSession levelSession,
            PlayerStealthState playerStealth, SupportItemController playerSupportItems = null)
        {
            UnsubscribeGameplayEvents();
            interactor = playerInteractor;
            session = levelSession;
            stealth = playerStealth;
            supportItems = playerSupportItems;
            SubscribeGameplayEvents();
        }

        public bool PlayAction(string action, bool blockGameplayInput = true, bool returnToLocomotion = true)
        {
            var direction = motor != null ? motor.FacingDirection : Vector2.down;
            return PlayAction(action, direction, blockGameplayInput, returnToLocomotion);
        }

        public bool PlayAction(string action, Vector2 direction, bool blockGameplayInput = true, bool returnToLocomotion = true)
        {
            if (!TryGetDefinition(action, out var definition) || animator == null) return false;
            var stateHash = LayerStateHash(StateName(action, DirectionName(direction)));
            if (!animator.HasState(0, stateHash)) return false;

            CancelCurrentAction(true);
            BeginAction(action, stateHash, blockGameplayInput);
            var duration = Mathf.Max(0.01f, definition.frameCount / (float)Mathf.Max(1, definition.fps));
            _completion = StartCoroutine(FinishAfter(action, duration, returnToLocomotion));
            return true;
        }

        public bool PlayLoop(string action, bool blockGameplayInput = false)
        {
            if (!TryGetDefinition(action, out var definition) || !definition.loop || animator == null) return false;
            var direction = motor != null ? motor.FacingDirection : Vector2.down;
            var stateHash = LayerStateHash(StateName(action, DirectionName(direction)));
            if (!animator.HasState(0, stateHash)) return false;

            CancelCurrentAction(true);
            BeginAction(action, stateHash, blockGameplayInput);
            return true;
        }

        public void StopAction(bool returnToLocomotion = true)
        {
            if (_completion != null)
            {
                StopCoroutine(_completion);
                _completion = null;
            }
            IsPlayingAction = false;
            CurrentAction = null;
            if (returnToLocomotion && animator != null)
            {
                var locomotionHash = LayerStateHash("Rook Locomotion");
                if (animator.HasState(0, locomotionHash)) animator.Play(locomotionHash, 0, 0f);
            }
            RestoreGameplayInput();
        }

        private IEnumerator FinishAfter(string action, float duration, bool returnToLocomotion)
        {
            yield return new WaitForSeconds(duration);
            _completion = null;
            IsPlayingAction = false;
            if (returnToLocomotion)
            {
                var locomotionHash = LayerStateHash("Rook Locomotion");
                if (animator != null && animator.HasState(0, locomotionHash)) animator.Play(locomotionHash, 0, 0f);
                CurrentAction = null;
                RestoreGameplayInput();
            }
            if (action == "hide-enter" && stealth != null && stealth.IsHidden && spriteRenderer != null)
                spriteRenderer.enabled = false;
            ActionCompleted?.Invoke(action);
        }

        private void BeginAction(string action, int stateHash, bool blockGameplayInput)
        {
            if (blockGameplayInput && input != null)
            {
                _previousGameplayBlocked = input.GameplayBlocked;
                input.SetGameplayBlocked(true);
                _gameplayBlockRevision = input.GameplayBlockRevision;
                _ownsGameplayBlock = true;
            }
            else _ownsGameplayBlock = false;

            animator.Play(stateHash, 0, 0f);
            CurrentAction = action;
            IsPlayingAction = true;
        }

        private void CancelCurrentAction(bool returnToLocomotion)
        {
            if (_completion != null)
            {
                StopCoroutine(_completion);
                _completion = null;
            }
            if (returnToLocomotion && IsPlayingAction && animator != null)
            {
                var locomotionHash = LayerStateHash("Rook Locomotion");
                if (animator.HasState(0, locomotionHash)) animator.Play(locomotionHash, 0, 0f);
            }
            RestoreGameplayInput();
            if (CurrentAction == "hide-enter" && stealth != null && stealth.IsHidden && spriteRenderer != null)
                spriteRenderer.enabled = false;
            IsPlayingAction = false;
            CurrentAction = null;
        }

        private void RestoreGameplayInput()
        {
            if (!_ownsGameplayBlock || input == null) return;
            // Another modal may have taken ownership while this action was playing.
            if (input.GameplayBlockRevision == _gameplayBlockRevision && input.GameplayBlocked)
                input.SetGameplayBlocked(_previousGameplayBlocked);
            _ownsGameplayBlock = false;
        }

        private void OnEnable() => SubscribeGameplayEvents();

        private void OnDisable()
        {
            UnsubscribeGameplayEvents();
            StopAction();
        }

        private void SubscribeGameplayEvents()
        {
            if (_subscribed) return;
            if (interactor != null) interactor.Interacted += OnInteracted;
            if (stealth != null) stealth.Captured += OnCaptured;
            if (supportItems != null) supportItems.ProjectileLaunched += OnProjectileLaunched;
            _subscribed = true;
        }

        private void UnsubscribeGameplayEvents()
        {
            if (!_subscribed) return;
            if (interactor != null) interactor.Interacted -= OnInteracted;
            if (stealth != null) stealth.Captured -= OnCaptured;
            if (supportItems != null) supportItems.ProjectileLaunched -= OnProjectileLaunched;
            _subscribed = false;
        }

        private void OnInteracted(string stableId)
        {
            if (stableId == "vent-access")
            {
                if (session != null && session.Progress != null && session.Progress.IsCompleted("open-vent"))
                    PlayAction("pry");
                else
                    PlayAction("interact");
                return;
            }

            if (stableId == "cell-locker" || stableId == "office-locker")
            {
                var entering = stealth != null && stealth.IsHidden;
                if (entering && spriteRenderer != null) spriteRenderer.enabled = true;
                if (!PlayAction(entering ? "hide-enter" : "hide-exit") && entering && spriteRenderer != null)
                    spriteRenderer.enabled = false;
                return;
            }

            PlayAction("interact");
        }

        private void OnProjectileLaunched(Vector2 direction) => PlayAction("throw", direction, blockGameplayInput: false);
        private void OnCaptured() => PlayAction("caught", blockGameplayInput: true, returnToLocomotion: false);

        private bool TryGetDefinition(string action, out ActionDefinition definition)
        {
            if (_byName.Count == 0) RebuildLookup();
            if (!string.IsNullOrWhiteSpace(action)) return _byName.TryGetValue(action, out definition);
            definition = default;
            return false;
        }

        public bool TryGetActionDuration(string action, out float duration)
        {
            if (!TryGetDefinition(action, out var definition))
            {
                duration = 0f;
                return false;
            }
            duration = definition.frameCount / (float)Mathf.Max(1, definition.fps);
            return true;
        }

        private void RebuildLookup()
        {
            _byName.Clear();
            if (actions == null) return;
            foreach (var definition in actions)
                if (!string.IsNullOrWhiteSpace(definition.action)) _byName[definition.action] = definition;
        }

        public static string StateName(string action, string direction) => "Action_" + action.Replace('-', '_') + "_" + direction;
        private static int LayerStateHash(string stateName) => Animator.StringToHash("Base Layer." + stateName);

        public static string DirectionName(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.down;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            var octant = Mathf.RoundToInt(angle / 45f) % 8;
            switch (octant)
            {
                case 0: return "E";
                case 1: return "NE";
                case 2: return "N";
                case 3: return "NW";
                case 4: return "W";
                case 5: return "SW";
                case 6: return "S";
                default: return "SE";
            }
        }
    }
}
