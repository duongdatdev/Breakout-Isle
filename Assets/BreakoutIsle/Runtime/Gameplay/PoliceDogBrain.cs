using System.Collections.Generic;
using BreakoutIsle.Audio;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PoliceDogBrain : MonoBehaviour
    {
        private static readonly List<PoliceDogBrain> Active = new List<PoliceDogBrain>();

        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0.1f)] private float moveSpeed = 7f;
        [SerializeField, Min(0.1f)] private float smellRadius = ScentTrailSystem.SmellRadius;
        [SerializeField, Min(0.1f)] private float arrivalDistance = 0.22f;
        [SerializeField, Min(0f)] private float guardAlertRadius = 15f;
        [SerializeField, Min(0f)] private float barkNoiseRadius = 10f;

        private Rigidbody2D _body;
        private Vector2 _baselinePosition;
        private readonly List<Vector2> _path = new List<Vector2>();
        private int _pathIndex;
        private float _pathRefreshTimer;
        private Vector2 _plannedGoal;
        private Vector2 _facing = Vector2.down;
        private MeatDistractionSource _foodSource;
        private bool _hasScentTarget;
        private ScentTrailSystem.Breadcrumb _scentTarget;
        private long _lastReachedScentId;
        private long _pendingScentId;
        private float _lastBarkAt = float.NegativeInfinity;
        private float _barkUntil;
        private float _sniffUntil;

        public float SmellRadius => smellRadius;
        public bool IsEating => _foodSource != null && _foodSource.IsEating(this, Time.time);
        public Vector2 Facing => _facing;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _baselinePosition = _body.position;
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            if (animator == null) animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (_foodSource != null) _foodSource.ReleaseClaim(this);
            _foodSource = null;
            StopMoving();
        }

        private void Update()
        {
            _pathRefreshTimer -= Time.deltaTime;
            if (_foodSource != null && _foodSource.IsExpired(Time.time))
                _foodSource = null;
            if (_foodSource != null && _foodSource.IsEating(this, Time.time))
            {
                StopMoving();
                SetMode(5f); // generated PoliceDog eat state
                return;
            }

            if (Time.time < _barkUntil)
            {
                StopMoving();
                SetMode(4f); // bark
                return;
            }
            if (Time.time < _sniffUntil)
            {
                StopMoving();
                SetMode(3f); // sniff
                return;
            }

            if (_foodSource == null && MeatDistractionSource.TryClaimNearest(this, _body.position, smellRadius,
                    Time.time, out var foodSource))
            {
                _foodSource = foodSource;
                _hasScentTarget = false;
                StopMoving();
            }
            if (_foodSource != null)
            {
                if (Vector2.Distance(_body.position, _foodSource.Position) <= arrivalDistance)
                {
                    StopMoving();
                    if (_foodSource.TryStartEating(this, Time.time))
                    {
                        SetMode(5f);
                        return;
                    }
                    _foodSource.ReleaseClaim(this);
                    _foodSource = null;
                }
                else
                {
                    MoveToward(_foodSource.Position);
                    SetMode(2f);
                    return;
                }
            }

            if (_hasScentTarget && Time.time - _scentTarget.createdAt > ScentTrailSystem.LifetimeSeconds)
                _hasScentTarget = false;
            if (_hasScentTarget)
            {
                if (Vector2.Distance(_body.position, _scentTarget.position) <= arrivalDistance)
                {
                    _lastReachedScentId = _pendingScentId;
                    _hasScentTarget = false;
                }
                else
                {
                    MoveToward(_scentTarget.position);
                    SetMode(2f);
                    return;
                }
            }

            if (ScentTrailSystem.TryGetNext(_lastReachedScentId, _body.position, smellRadius, Time.time,
                    out var nextBreadcrumb))
            {
                _scentTarget = nextBreadcrumb;
                _pendingScentId = nextBreadcrumb.id;
                _hasScentTarget = true;
                BarkAndAlertGuards();
                _sniffUntil = Time.time + 0.7f;
                SetMode(3f);
                return;
            }

            StopMoving();
            SetMode(0f);
        }

        private void FixedUpdate()
        {
            if (_body == null || _pathIndex >= _path.Count) return;
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
            _facing = delta.normalized;
            _body.MovePosition(_body.position + _facing * (moveSpeed * Time.fixedDeltaTime));
            if (animator != null)
            {
                animator.SetFloat("FacingX", _facing.x);
                animator.SetFloat("FacingY", _facing.y);
            }
        }

        private void MoveToward(Vector2 destination)
        {
            if (_pathRefreshTimer > 0f && (destination - _plannedGoal).sqrMagnitude < 0.16f) return;
            _plannedGoal = destination;
            _pathRefreshTimer = 0.2f;
            _pathIndex = 0;
            GuardPathfinding.FindPath(_body.position, destination, obstacleMask, _path);
        }

        private void StopMoving()
        {
            _path.Clear();
            _pathIndex = 0;
            _pathRefreshTimer = 0f;
            if (_body != null) _body.linearVelocity = Vector2.zero;
        }

        private void BarkAndAlertGuards()
        {
            if (Time.time - _lastBarkAt < 3f) return;
            _lastBarkAt = Time.time;
            _barkUntil = Time.time + 0.4f;
            NoiseSystem.Emit(_body.position, barkNoiseRadius);
            GuardBrain.TriggerAlarm(_body.position, guardAlertRadius);
            if (AudioService.Instance != null) AudioService.Instance.PlayAt("animal.dog.bark", _body.position);
            SetMode(4f);
        }

        private void SetMode(float value)
        {
            if (animator == null) return;
            animator.SetFloat("DogMode", value); // 0 idle, 1 walk, 2 run, 3 sniff, 4 bark, 5 eat
            animator.SetFloat("FacingX", _facing.x);
            animator.SetFloat("FacingY", _facing.y);
        }

        public void ResetTransientState()
        {
            if (_foodSource != null) _foodSource.ReleaseClaim(this);
            _foodSource = null;
            StopMoving();
            if (_body != null)
            {
                _body.position = _baselinePosition;
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
                transform.position = _baselinePosition;
            }
            _hasScentTarget = false;
            _scentTarget = default;
            _lastReachedScentId = 0;
            _pendingScentId = 0;
            _lastBarkAt = float.NegativeInfinity;
            _barkUntil = 0f;
            _sniffUntil = 0f;
            if (animator != null)
            {
                _facing = Vector2.down;
                SetMode(0f);
            }
        }

        public static void ResetAllTransientState()
        {
            foreach (var dog in Active)
                if (dog != null) dog.ResetTransientState();
        }
    }
}
