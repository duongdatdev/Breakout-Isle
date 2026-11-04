using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public enum GuardState
    {
        Patrol,
        Suspicious,
        Chase,
        Search,
        Alert
    }

    public static class GuardDetectionMath
    {
        public static float SecondsToDetect(float distance, float visionRange)
        {
            var t = Mathf.InverseLerp(1f, Mathf.Max(1.01f, visionRange), distance);
            return Mathf.Lerp(0.5f, 1.5f, t);
        }

        public static float DetectionRate(float distance, float visionRange, bool sneaking, bool inDarkZone)
        {
            var rate = 1f / SecondsToDetect(distance, visionRange);
            if (sneaking)
                rate *= 0.7f;
            if (inDarkZone)
                rate *= 0.3f;
            return rate;
        }
    }

    public static class NoiseSystem
    {
        public static event Action<Vector2, float> Emitted;

        public static void Emit(Vector2 position, float radius) => Emitted?.Invoke(position, radius);
    }

    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class GuardBrain : MonoBehaviour
    {
        private static readonly List<GuardBrain> Active = new List<GuardBrain>();

        [SerializeField] private PlayerStealthState player;
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private LayerMask lineOfSightObstacles;
        [SerializeField] private float visionRange = 6f;
        [SerializeField] private float visionAngle = 90f;
        [SerializeField] private float patrolSpeed = 2.5f;
        [SerializeField] private float chaseSpeed = 5.5f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;
        [SerializeField] private GuardVisionIndicator visionIndicator;

        private Rigidbody2D _body;
        private Vector2 _baselinePosition;
        private int _patrolIndex;
        private Vector2 _forward = Vector2.down;
        private Vector2 _lastKnownPosition;
        private Vector2 _investigatePosition;
        private HidingSpot _witnessedSpot;
        private float _detection;
        private float _lostSightTime;
        private float _searchTime;
        private float _alertTime;
        private bool _searchedOneSpot;
        private HidingSpot _searchSpot;
        private readonly List<Vector2> _path = new List<Vector2>();
        private int _pathIndex;
        private Vector2 _plannedGoal;
        private float _pathRefreshTimer;
        private float _moveSpeed;
        private bool _hasMoveGoal;
        private float _visionBonusTimer;
        private float _captureAnimationTimer;

        public GuardState State { get; private set; } = GuardState.Patrol;
        public float Detection => _detection;
        public Vector2 LastKnownPosition => _lastKnownPosition;
        public float VisionRange => visionRange * (State == GuardState.Alert || _visionBonusTimer > 0f ? 1.2f : 1f);
        public event Action<GuardState> StateChanged;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _baselinePosition = _body.position;
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null)
                animator = GetComponent<Animator>();
            if (visionIndicator == null)
                visionIndicator = GetComponent<GuardVisionIndicator>();
            if (visionIndicator != null)
                visionIndicator.SetObstacleMask(lineOfSightObstacles);
            if (player == null)
                player = FindAnyObjectByType<PlayerStealthState>();
        }

        private void OnEnable()
        {
            if (!Active.Contains(this))
                Active.Add(this);
            NoiseSystem.Emitted += OnNoise;
        }

        private void OnDisable()
        {
            Active.Remove(this);
            NoiseSystem.Emitted -= OnNoise;
        }

        private void Update()
        {
            _pathRefreshTimer -= Time.deltaTime;
            _visionBonusTimer = Mathf.Max(0f, _visionBonusTimer - Time.deltaTime);
            if (_captureAnimationTimer > 0f)
            {
                _captureAnimationTimer -= Time.deltaTime;
                if (_captureAnimationTimer <= 0f) SetAnimatorMode(0f);
            }
            if (player == null || player.IsCaptured)
            {
                StopMoving();
                if (animator != null && _captureAnimationTimer <= 0f) SetAnimatorMode(0f);
                return;
            }

            var seesPlayer = CanSeePlayer(out var distance);
            switch (State)
            {
                case GuardState.Patrol:
                case GuardState.Suspicious:
                case GuardState.Alert:
                    UpdateAwareness(seesPlayer, distance);
                    if (State == GuardState.Patrol || State == GuardState.Alert)
                        Patrol();
                    else if (State == GuardState.Suspicious)
                        MoveToward(_investigatePosition, patrolSpeed);
                    break;
                case GuardState.Chase:
                    UpdateChase(seesPlayer, distance);
                    break;
                case GuardState.Search:
                    UpdateSearch(seesPlayer, distance);
                    break;
            }

            if (animator != null)
            {
                animator.SetFloat("FacingX", _forward.x);
                animator.SetFloat("FacingY", _forward.y);
                var moving = _hasMoveGoal && _pathIndex < _path.Count && _moveSpeed > 0f;
                if (_captureAnimationTimer <= 0f)
                    SetAnimatorMode(!moving ? 0f : State == GuardState.Chase ? 2f :
                        State == GuardState.Search || State == GuardState.Suspicious || State == GuardState.Alert ? 3f : 1f);
            }
            if (visionIndicator != null) visionIndicator.SetObstacleMask(lineOfSightObstacles);
            visionIndicator?.SetView(_forward, VisionRange, visionAngle, State, _detection);
        }

        private void FixedUpdate()
        {
            if (!_hasMoveGoal || _pathIndex >= _path.Count || _moveSpeed <= 0f || _body == null)
                return;
            var waypoint = _path[_pathIndex];
            var delta = waypoint - _body.position;
            if (delta.sqrMagnitude <= 0.04f)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count) return;
                waypoint = _path[_pathIndex];
                delta = waypoint - _body.position;
            }
            if (delta.sqrMagnitude <= 0.0001f) return;
            _forward = delta.normalized;
            _body.MovePosition(_body.position + _forward * (_moveSpeed * Time.fixedDeltaTime));
        }

        private void UpdateAwareness(bool seesPlayer, float distance)
        {
            if (seesPlayer)
            {
                _lastKnownPosition = player.transform.position;
                _investigatePosition = _lastKnownPosition;
                var stealth = player.GetComponent<PlayerMotor>();
                var darkZone = player.GetComponent<PlayerStealthZone>();
                var rate = GuardDetectionMath.DetectionRate(distance, VisionRange, stealth != null && stealth.IsSneaking, darkZone != null && darkZone.IsInDarkZone);
                _detection = Mathf.Clamp01(_detection + rate * Time.deltaTime);
                if (_detection >= 0.5f && State != GuardState.Chase)
                    SetState(GuardState.Suspicious);
                if (_detection >= 1f)
                    BeginChase();
                return;
            }

            _detection = Mathf.MoveTowards(_detection, 0f, Time.deltaTime * 0.5f);
            if (State == GuardState.Suspicious && Vector2.Distance(transform.position, _investigatePosition) < 0.25f)
                SetState(GuardState.Patrol);
            if (State == GuardState.Alert)
            {
                _alertTime -= Time.deltaTime;
                if (_alertTime <= 0f)
                    SetState(GuardState.Patrol);
            }
        }

        private void UpdateChase(bool seesPlayer, float distance)
        {
            if (seesPlayer)
            {
                _lastKnownPosition = player.transform.position;
                _lostSightTime = 0f;
                var stealth = player.GetComponent<PlayerMotor>();
                var darkZone = player.GetComponent<PlayerStealthZone>();
                var rate = GuardDetectionMath.DetectionRate(distance, VisionRange, stealth != null && stealth.IsSneaking, darkZone != null && darkZone.IsInDarkZone);
                _detection = Mathf.Clamp01(_detection + rate * Time.deltaTime);
                MoveToward(_lastKnownPosition, chaseSpeed);
                return;
            }

            _detection = Mathf.MoveTowards(_detection, 0f, Time.deltaTime * 0.5f);
            _lostSightTime += Time.deltaTime;
            MoveToward(_lastKnownPosition, chaseSpeed);
            if (_lostSightTime >= 3f)
            {
                _searchTime = 10f;
                _searchedOneSpot = false;
                _searchSpot = null;
                SetState(GuardState.Search);
            }
        }

        private void UpdateSearch(bool seesPlayer, float distance)
        {
            if (seesPlayer)
            {
                _lastKnownPosition = player.transform.position;
                BeginChase();
                return;
            }

            _searchTime -= Time.deltaTime;
            if (!_searchedOneSpot)
            {
                if (_searchSpot == null)
                    _searchSpot = _witnessedSpot != null ? _witnessedSpot : HidingSpot.FindNearest(_lastKnownPosition, 2f);
                if (_searchSpot != null)
                {
                    MoveToward(_searchSpot.InspectionPosition, patrolSpeed);
                    if (CanInspect(_searchSpot))
                    {
                        _searchedOneSpot = true;
                        if (player.IsHidden && player.CurrentHidingSpot == _searchSpot) player.Capture();
                        _searchSpot = null;
                        _witnessedSpot = null;
                    }
                }
                else
                {
                    MoveToward(_lastKnownPosition, patrolSpeed);
                    if (Vector2.Distance(transform.position, _lastKnownPosition) < 0.45f)
                        _searchedOneSpot = true;
                }
            }
            if (_searchTime <= 0f)
            {
                _alertTime = 30f;
                SetState(GuardState.Alert);
            }
        }

        private void Patrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                StopMoving();
                return;
            }
            var point = patrolPoints[_patrolIndex];
            if (point == null)
            {
                StopMoving();
                return;
            }
            MoveToward(point.position, patrolSpeed);
            if (Vector2.Distance(transform.position, point.position) < 0.15f)
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
        }

        private void MoveToward(Vector2 destination, float speed)
        {
            _moveSpeed = speed;
            _hasMoveGoal = true;
            if (_pathRefreshTimer > 0f && (destination - _plannedGoal).sqrMagnitude < 0.16f)
                return;
            _plannedGoal = destination;
            _pathRefreshTimer = 0.2f;
            _pathIndex = 0;
            GuardPathfinding.FindPath(_body.position, destination, lineOfSightObstacles, _path);
            if (_path.Count == 0)
                _hasMoveGoal = false;
        }

        private void StopMoving()
        {
            _hasMoveGoal = false;
            _path.Clear();
            _pathIndex = 0;
            _pathRefreshTimer = 0f;
            _moveSpeed = 0f;
            if (_body != null) _body.linearVelocity = Vector2.zero;
        }

        private bool CanSeePlayer(out float distance)
        {
            distance = float.PositiveInfinity;
            if (player == null || player.IsCaptured || player.IsHidden)
                return false;

            var offset = (Vector2)player.transform.position - _body.position;
            distance = offset.magnitude;
            if (distance > VisionRange || distance <= 0.001f)
                return false;
            if (Vector2.Angle(_forward, offset) > visionAngle * 0.5f)
                return false;
            return !Physics2D.Raycast(_body.position, offset / distance, distance, lineOfSightObstacles);
        }

        private void BeginChase()
        {
            if (State == GuardState.Chase)
                return;
            EnterChase(_lastKnownPosition);
            foreach (var guard in Active)
            {
                if (guard != this && Vector2.Distance(guard.transform.position, transform.position) <= 10f)
                    guard.JoinChase(_lastKnownPosition);
            }
        }

        private void EnterChase(Vector2 position)
        {
            _lastKnownPosition = position;
            _investigatePosition = position;
            _lostSightTime = 0f;
            _detection = 1f;
            SetState(GuardState.Chase);
        }

        private void JoinChase(Vector2 position)
        {
            if (State == GuardState.Chase) return;
            EnterChase(position);
        }

        private void Investigate(Vector2 position)
        {
            if (State == GuardState.Chase)
                return;
            _investigatePosition = position;
            SetState(GuardState.Suspicious);
        }

        private void OnNoise(Vector2 position, float radius)
        {
            if (Vector2.Distance(transform.position, position) <= radius && State != GuardState.Chase)
                Investigate(position);
        }

        public static bool IsPositionObserved(Vector2 position, PlayerStealthState candidate)
        {
            foreach (var guard in Active)
            {
                if (guard == null || guard.player != candidate)
                    continue;
                if (guard.CanObservePosition(position))
                    return true;
            }
            return false;
        }

        private bool CanObservePosition(Vector2 position)
        {
            var offset = position - (Vector2)transform.position;
            var distance = offset.magnitude;
            return distance > 0.001f && distance <= VisionRange && Vector2.Angle(_forward, offset) <= visionAngle * 0.5f &&
                   !Physics2D.Raycast(_body.position, offset / distance, distance, lineOfSightObstacles);
        }

        private bool CanInspect(HidingSpot spot)
        {
            if (spot == null) return false;
            var exitDelta = spot.InspectionPosition - _body.position;
            if (exitDelta.magnitude > 0.7f) return false;
            return exitDelta.sqrMagnitude <= 0.001f ||
                   !Physics2D.Raycast(_body.position, exitDelta.normalized, exitDelta.magnitude, lineOfSightObstacles);
        }

        public static void ResetAllToBaseline()
        {
            foreach (var guard in Active)
                if (guard != null) guard.ResetToBaseline();
        }

        private void ResetToBaseline()
        {
            StopMoving();
            _body.position = _baselinePosition;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            transform.position = _baselinePosition;
            _patrolIndex = 0;
            _forward = Vector2.down;
            _lastKnownPosition = _baselinePosition;
            _investigatePosition = _baselinePosition;
            _witnessedSpot = null;
            _detection = 0f;
            _lostSightTime = 0f;
            _searchTime = 0f;
            _alertTime = 0f;
            _searchedOneSpot = false;
            _searchSpot = null;
            _pathRefreshTimer = 0f;
            _visionBonusTimer = 0f;
            _captureAnimationTimer = 0f;
            SetState(GuardState.Patrol);
            if (animator != null)
            {
                animator.SetFloat("FacingX", _forward.x);
                animator.SetFloat("FacingY", _forward.y);
                SetAnimatorMode(0f);
            }
        }

        public static void NotifyHidingEntry(PlayerStealthState candidate, HidingSpot spot, bool witnessed)
        {
            if (!witnessed)
                return;
            foreach (var guard in Active)
            {
                if (guard.player == candidate && guard.CanObservePosition(candidate.transform.position))
                {
                    guard._witnessedSpot = spot;
                    guard._lastKnownPosition = spot.transform.position;
                    guard._searchTime = 10f;
                    guard.SetState(GuardState.Search);
                }
            }
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (State == GuardState.Chase && player != null && collision.gameObject == player.gameObject && !player.IsHidden)
            {
                _captureAnimationTimer = 0.6f;
                SetAnimatorMode(4f);
                player.Capture();
            }
        }

        private void SetAnimatorMode(float mode)
        {
            if (animator != null) animator.SetFloat("GuardMode", mode);
        }

        private void SetState(GuardState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke(state);
        }

        public void ReportAlarm(Vector2 position)
        {
            _visionBonusTimer = 30f;
            if (State != GuardState.Chase && State != GuardState.Search)
                Investigate(position);
        }

        public static void TriggerAlarm(Vector2 position, float radius)
        {
            foreach (var guard in Active)
                if (guard != null && Vector2.Distance(guard.transform.position, position) <= radius)
                    guard.ReportAlarm(position);
        }
    }

}
