using UnityEngine;
using UnityEngine.InputSystem;

namespace BreakoutIsle.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        private const float WalkSpeed = 3f;
        private const float SneakSpeed = 1.5f;
        private const float SprintSpeed = 6f;
        private const float InputDeadZone = 0.01f;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string sprintActionName = "Sprint";
        [SerializeField] private string sneakActionName = "Sneak";

        private Rigidbody2D _body;
        private InputActionMap _actionMap;
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _sneakAction;
        private Vector2 _moveInput;
        private bool _sprintHeld;
        private bool _sneakHeld;
        private readonly StaminaMeter _stamina = new StaminaMeter();

        public float Stamina => _stamina.Current;
        public Vector2 MoveInput => _moveInput;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;

            if (inputActions == null)
            {
                Debug.LogError("PlayerMotor needs the project's InputActionAsset assigned.", this);
                enabled = false;
                return;
            }

            _actionMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            _moveAction = _actionMap.FindAction(moveActionName, throwIfNotFound: true);
            _sprintAction = _actionMap.FindAction(sprintActionName, throwIfNotFound: true);
            _sneakAction = _actionMap.FindAction(sneakActionName, throwIfNotFound: true);
        }

        private void OnEnable()
        {
            if (_actionMap != null)
                _actionMap.Enable();
        }

        private void OnDisable()
        {
            if (_actionMap != null)
                _actionMap.Disable();
        }

        private void Update()
        {
            if (_moveAction == null)
                return;

            _moveInput = _moveAction.ReadValue<Vector2>();
            _sprintHeld = _sprintAction.IsPressed();
            _sneakHeld = _sneakAction.IsPressed();
        }

        private void FixedUpdate()
        {
            var movement = MovementMath.NormalizeInput(_moveInput);
            var isMoving = movement.sqrMagnitude > InputDeadZone * InputDeadZone;
            var sprinting = _stamina.Tick(_sprintHeld && !_sneakHeld && isMoving, Time.fixedDeltaTime);
            var speed = _sneakHeld ? SneakSpeed : sprinting ? SprintSpeed : WalkSpeed;
            _body.MovePosition(_body.position + movement * (speed * Time.fixedDeltaTime));
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
