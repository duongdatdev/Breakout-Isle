using System.Collections.Generic;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class HidingSpot : MonoBehaviour, IPlayerInteractable
    {
        private static readonly List<HidingSpot> Active = new List<HidingSpot>();
        [SerializeField] private string persistentId;
        [SerializeField] private string prompt = "Ẩn nấp";
        [SerializeField] private Transform exitPoint;
        public string StableId => persistentId;
        public string Prompt => prompt;
        public Vector2 ExitPosition => exitPoint != null ? (Vector2)exitPoint.position : (Vector2)transform.position;
        public Vector2 InspectionPosition => ExitPosition;
        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);
        public bool CanInteract(PlayerInteractor player)
        {
            var state = player != null ? player.GetComponent<PlayerStealthState>() : null;
            if (state == null) return false;
            return state.IsHidden
                ? state.CurrentHidingSpot == this && IsExitClear()
                : IsExitClear();
        }
        public void Interact(PlayerInteractor player)
        {
            var state = player.GetComponent<PlayerStealthState>();
            if (state.IsHidden) { state.ExitHidingSpot(ExitPosition); return; }
            var witnessed = GuardBrain.IsPositionObserved(player.transform.position, state);
            GuardBrain.NotifyHidingEntry(state, this, witnessed);
            state.EnterHidingSpot(this, witnessed);
        }

        private bool IsExitClear()
        {
            if (exitPoint == null) return false;
            foreach (var collider in Physics2D.OverlapCircleAll(ExitPosition, 0.22f))
            {
                if (collider == null || collider.isTrigger || collider.GetComponentInParent<HidingSpot>() == this)
                    continue;
                return false;
            }
            return true;
        }
        public static HidingSpot FindNearest(Vector2 position, float radius)
        {
            HidingSpot best = null;
            var bestDistance = radius * radius;
            foreach (var spot in Active)
            {
                if (spot == null) continue;
                var distance = ((Vector2)spot.transform.position - position).sqrMagnitude;
                if (distance > bestDistance) continue;
                best = spot;
                bestDistance = distance;
            }
            return best;
        }
    }
}
