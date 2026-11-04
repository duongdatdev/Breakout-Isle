using System;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public interface IPlayerInteractable
    {
        string StableId { get; }
        string Prompt { get; }
        bool CanInteract(PlayerInteractor player);
        void Interact(PlayerInteractor player);
    }

    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private LevelSession session;
        [SerializeField] private LayerMask interactionMask;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField, Min(0.1f)] private float interactionRange = 1.35f;

        private PlayerInputReader _input;
        private PlayerStealthState _stealthState;
        private IPlayerInteractable _current;
        public LevelSession Session => session;
        public IPlayerInteractable Current => _current;
        public event Action<string> PromptChanged;
        public event Action<string> Interacted;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _stealthState = GetComponent<PlayerStealthState>();
            if (_input == null || session == null)
                Debug.LogError("PlayerInteractor needs PlayerInputReader and LevelSession references.", this);
        }

        private void OnEnable() { if (_input != null) _input.InteractPressed += TryInteract; }
        private void OnDisable() { if (_input != null) _input.InteractPressed -= TryInteract; }

        private void Update()
        {
            var next = FindClosestInteractable();
            if (ReferenceEquals(next, _current)) return;
            _current = next;
            PromptChanged?.Invoke(_current != null ? _current.Prompt : string.Empty);
        }

        public void TryInteract()
        {
            var target = FindClosestInteractable();
            if (target != null && target.CanInteract(this))
            {
                target.Interact(this);
                Interacted?.Invoke(target.StableId);
            }
        }

        private IPlayerInteractable FindClosestInteractable()
        {
            if (_stealthState != null && _stealthState.IsCaptured)
                return null;
            var mask = interactionMask.value == 0 ? Physics2D.AllLayers : interactionMask.value;
            var colliders = Physics2D.OverlapCircleAll(transform.position, interactionRange, mask);
            var playerColliders = GetComponentsInChildren<Collider2D>();
            var bestDistance = float.PositiveInfinity;
            IPlayerInteractable best = null;
            foreach (var collider in colliders)
            foreach (var behaviour in collider.GetComponentsInParent<MonoBehaviour>())
            {
                if (!(behaviour is IPlayerInteractable candidate) || !candidate.CanInteract(this)) continue;
                var targetPosition = behaviour.transform.position;
                var distance = ((Vector2)targetPosition - (Vector2)transform.position).sqrMagnitude;
                if (distance >= bestDistance || IsOccluded(targetPosition, collider, behaviour.transform, playerColliders)) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        private bool IsOccluded(Vector2 targetPosition, Collider2D targetCollider, Transform targetTransform, Collider2D[] playerColliders)
        {
            var offset = targetPosition - (Vector2)transform.position;
            var distance = offset.magnitude;
            if (distance <= Mathf.Epsilon) return false;
            var mask = obstacleMask.value == 0 ? Physics2D.AllLayers : obstacleMask.value;
            foreach (var hit in Physics2D.RaycastAll(transform.position, offset / distance, distance, mask))
            {
                if (hit.collider == null || hit.collider == targetCollider ||
                    hit.collider.transform == targetTransform || hit.collider.transform.IsChildOf(targetTransform)) continue;
                var isPlayerCollider = false;
                foreach (var ownCollider in playerColliders)
                    if (hit.collider == ownCollider) { isPlayerCollider = true; break; }
                if (!isPlayerCollider) return true;
            }
            return false;
        }
    }
}
