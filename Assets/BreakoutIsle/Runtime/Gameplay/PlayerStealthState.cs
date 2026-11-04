using System;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class PlayerStealthState : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Collider2D _collider;
        private PlayerMotor _motor;

        public bool IsHidden { get; private set; }
        public bool HidingEntryWitnessed { get; private set; }
        public bool IsCaptured { get; private set; }
        public HidingSpot CurrentHidingSpot { get; private set; }
        public event Action Captured;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();
            _motor = GetComponent<PlayerMotor>();
        }

        public void EnterHidingSpot(HidingSpot spot, bool witnessed)
        {
            if (spot == null || IsCaptured)
                return;
            IsHidden = true;
            HidingEntryWitnessed = witnessed;
            CurrentHidingSpot = spot;
            if (_renderer != null)
                _renderer.enabled = false;
            if (_collider != null)
                _collider.enabled = false;
            if (_motor != null)
                _motor.enabled = false;
            transform.position = spot.transform.position;
        }

        public void ExitHidingSpot(Vector2 exitPosition)
        {
            if (!IsHidden || IsCaptured)
                return;
            transform.position = exitPosition;
            IsHidden = false;
            HidingEntryWitnessed = false;
            CurrentHidingSpot = null;
            if (_renderer != null)
                _renderer.enabled = true;
            if (_collider != null)
                _collider.enabled = true;
            if (_motor != null)
                _motor.enabled = true;
        }

        public void Capture()
        {
            if (IsCaptured)
                return;
            IsCaptured = true;
            IsHidden = false;
            if (_renderer != null)
                _renderer.enabled = true;
            if (_collider != null)
                _collider.enabled = false;
            if (_motor != null)
                _motor.enabled = false;
            GetComponent<PlayerInputReader>()?.SetGameplayBlocked(true);
            Captured?.Invoke();
        }

        public void ResetTransientState(Vector3 position)
        {
            IsCaptured = false;
            IsHidden = false;
            HidingEntryWitnessed = false;
            CurrentHidingSpot = null;
            transform.position = position;
            if (_renderer != null) _renderer.enabled = true;
            if (_collider != null) _collider.enabled = true;
            if (_motor != null) _motor.enabled = true;
            GetComponent<PlayerInputReader>()?.SetGameplayBlocked(false);
        }
    }
}
