using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        private const float WalkSpeed = 3f;
        private const float SneakSpeed = 1.5f;
        private const float SprintSpeed = 6f;
        private const float InputDeadZone = 0.01f;

        private Rigidbody2D _body;
        private Animator _animator;
        private PlayerInputReader _input;
        private Vector2 _facing = Vector2.down;
        private readonly StaminaMeter _stamina = new StaminaMeter();

        public float Stamina => _stamina.Current;
        public bool IsSneaking => _input != null && _input.SneakHeld;
        public Vector2 MoveInput => _input == null ? Vector2.zero : _input.Move;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _input = GetComponent<PlayerInputReader>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;

            if (_input == null)
            {
                Debug.LogError("PlayerMotor needs a PlayerInputReader on the same GameObject.", this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            var movement = MovementMath.NormalizeInput(_input == null ? Vector2.zero : _input.Move);
            var isMoving = movement.sqrMagnitude > InputDeadZone * InputDeadZone;
            var sneaking = _input != null && _input.SneakHeld;
            var sprinting = _stamina.Tick(_input != null && _input.SprintHeld && !sneaking && isMoving, Time.fixedDeltaTime);
            var speed = sneaking ? SneakSpeed : sprinting ? SprintSpeed : WalkSpeed;
            _body.MovePosition(_body.position + movement * (speed * Time.fixedDeltaTime));

            if (_animator != null)
            {
                if (isMoving)
                    _facing = movement.normalized;
                _animator.SetFloat("FacingX", _facing.x);
                _animator.SetFloat("FacingY", _facing.y);
                _animator.SetFloat("MoveMode", !isMoving ? 0f : sneaking ? 3f : sprinting ? 2f : 1f);
            }
        }
    }

    public static class MovementMath
    {
        public static Vector2 NormalizeInput(Vector2 input)
        {
            return input.sqrMagnitude > 1f ? input.normalized : input;
        }
    }
}
