using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : MonoBehaviour, ICheckpointPlayerAdapter
    {
        private const float WalkSpeed = 3f;
        private const float SneakSpeed = 1.5f;
        private const float SprintSpeed = 6f;
        private const float InputDeadZone = 0.01f;

        private Rigidbody2D _body;
        private Animator _animator;
        private PlayerInputReader _input;
        private Vector2 _facing = Vector2.down;
        private Vector2 _previousPhysicsPosition;
        private readonly StaminaMeter _stamina = new StaminaMeter();
        private float _footstepNoiseTimer;
        private bool _lastStepSneaking;
        private bool _lastStepSprintRequested;
        private bool _lastStepFreeSprint;
        private float _freeSprintTime;

        public float Stamina => _stamina.Current;
        public Vector2 FacingDirection => _facing;
        public bool IsSneaking => _input != null && _input.SneakHeld;
        public Vector2 MoveInput => _input == null ? Vector2.zero : _input.Move;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _input = GetComponent<PlayerInputReader>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _previousPhysicsPosition = _body.position;

            if (_input == null)
            {
                Debug.LogError("PlayerMotor needs a PlayerInputReader on the same GameObject.", this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            var actualDelta = _body.position - _previousPhysicsPosition;
            _previousPhysicsPosition = _body.position;
            var actuallyMoving = actualDelta.sqrMagnitude > InputDeadZone * InputDeadZone;
            var staminaSprint = _stamina.Tick(_lastStepSprintRequested && actuallyMoving && !_lastStepFreeSprint, Time.fixedDeltaTime);
            var sprintedThisStep = actuallyMoving && _lastStepSprintRequested && (_lastStepFreeSprint || staminaSprint);
            EmitFootstepNoise(actuallyMoving, _lastStepSneaking, sprintedThisStep, Time.fixedDeltaTime);

            var movement = MovementMath.NormalizeInput(_input == null ? Vector2.zero : _input.Move);
            var sneaking = _input != null && _input.SneakHeld;
            var hasMoveInput = movement.sqrMagnitude > InputDeadZone * InputDeadZone;
            var sprintRequested = _input != null && _input.SprintHeld && !sneaking && hasMoveInput;
            var freeSprint = _freeSprintTime > 0f;
            var sprinting = sprintRequested && (freeSprint || _stamina.CanSprint);
            var speed = sneaking ? SneakSpeed : sprinting ? SprintSpeed : WalkSpeed;
            if (hasMoveInput)
                _body.MovePosition(_body.position + movement * (speed * Time.fixedDeltaTime));
            _lastStepSneaking = sneaking;
            _lastStepSprintRequested = sprintRequested;
            _lastStepFreeSprint = sprinting && freeSprint;
            _freeSprintTime = Mathf.Max(0f, _freeSprintTime - Time.fixedDeltaTime);

            if (_animator != null)
            {
                if (actuallyMoving)
                    _facing = actualDelta.normalized;
                _animator.SetFloat("FacingX", _facing.x);
                _animator.SetFloat("FacingY", _facing.y);
                _animator.SetFloat("MoveMode", !actuallyMoving ? 0f : _lastStepSneaking ? 3f : sprintedThisStep ? 2f : 1f);
            }
        }

        private void EmitFootstepNoise(bool moving, bool sneaking, bool sprinting, float deltaTime)
        {
            var radius = sneaking ? 0f : sprinting ? 8f : 3f;
            if (!moving || radius <= 0f)
            {
                _footstepNoiseTimer = 0f;
                return;
            }

            _footstepNoiseTimer -= deltaTime;
            if (_footstepNoiseTimer > 0f) return;
            NoiseSystem.Emit(_body.position, radius);
            _footstepNoiseTimer = sprinting ? 0.28f : 0.42f;
        }

        public void ApplyEnergyDrink()
        {
            _stamina.RestoreFull();
            _freeSprintTime = 5f;
        }

        public void RestoreAt(Vector3 position)
        {
            var stealthState = GetComponent<PlayerStealthState>();
            if (stealthState != null) stealthState.ResetTransientState(position);
            else transform.position = position;
            GetComponent<PlayerActionAnimator>()?.StopAction();
            if (_body != null)
            {
                _body.position = position;
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
            }
            _stamina.RestoreFull();
            _footstepNoiseTimer = 0f;
            _previousPhysicsPosition = position;
            _lastStepSneaking = false;
            _lastStepSprintRequested = false;
            _lastStepFreeSprint = false;
            _freeSprintTime = 0f;
            _facing = Vector2.down;
            if (_animator != null)
            {
                _animator.SetFloat("FacingX", _facing.x);
                _animator.SetFloat("FacingY", _facing.y);
                _animator.SetFloat("MoveMode", 0f);
            }
            _input?.ResetGameplayInputUntilNeutral();
            _input?.SetGameplayBlocked(false);
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
