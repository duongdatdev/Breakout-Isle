using System.Collections.Generic;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    /// <summary>Transient landed food: visible and claimable for eight seconds after landing.</summary>
    public sealed class MeatDistractionSource : MonoBehaviour
    {
        public const float AvailabilitySeconds = 8f;
        public const float EatingSeconds = 8f;

        private static readonly List<MeatDistractionSource> Active = new List<MeatDistractionSource>();

        [SerializeField] private SpriteRenderer spriteRenderer;
        private readonly MeatDistractionWindow _window = new MeatDistractionWindow();
        private bool _landed;

        public Vector2 Position => transform.position;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.enabled = false;
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            if (_landed && _window.IsExpired(Time.time)) Destroy(gameObject);
        }

        public void Land(Vector2 position, Sprite foodSprite, float now)
        {
            if (_landed) return;
            _landed = true;
            if (!Active.Contains(this)) Active.Add(this);
            transform.position = position;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = foodSprite;
                spriteRenderer.enabled = foodSprite != null;
            }
            _window.Land(now, AvailabilitySeconds);
        }

        public bool IsEating(PoliceDogBrain dog, float now) => _window.IsEating(dog, now);
        public bool IsExpired(float now) => _window.IsExpired(now);
        public bool TryStartEating(PoliceDogBrain dog, float now) => _window.TryStartEating(dog, now);
        public void ReleaseClaim(PoliceDogBrain dog) => _window.ReleaseClaim(dog);

        public static bool TryClaimNearest(PoliceDogBrain dog, Vector2 position, float smellRadius, float now,
            out MeatDistractionSource source)
        {
            source = null;
            if (dog == null) return false;
            var radiusSquared = Mathf.Max(0f, smellRadius) * Mathf.Max(0f, smellRadius);
            var closestSquared = radiusSquared;
            foreach (var candidate in Active)
            {
                if (candidate == null || !candidate._landed || !candidate._window.CanClaim(now)) continue;
                var distanceSquared = (candidate.Position - position).sqrMagnitude;
                if (distanceSquared > closestSquared) continue;
                source = candidate;
                closestSquared = distanceSquared;
            }
            if (source == null || !source._window.TryClaim(dog, now))
            {
                source = null;
                return false;
            }
            return true;
        }

        public static void ResetAllTransientSources()
        {
            var sources = new List<MeatDistractionSource>(Active);
            foreach (var source in sources)
            {
                if (source == null) continue;
                if (Application.isPlaying) Destroy(source.gameObject);
                else DestroyImmediate(source.gameObject);
            }
            Active.Clear();
        }
    }

    /// <summary>Claim and timer state for one landed meat source.</summary>
    public sealed class MeatDistractionWindow
    {
        private object _claimant;
        private float _availableUntil;
        private float _eatingUntil;
        private bool _landed;

        public void Land(float now, float availabilitySeconds)
        {
            _landed = true;
            _claimant = null;
            _availableUntil = now + Mathf.Max(0f, availabilitySeconds);
            _eatingUntil = 0f;
        }

        public bool CanClaim(float now) => _landed && _claimant == null && now < _availableUntil;

        public bool TryClaim(object dog, float now)
        {
            if (dog == null || !CanClaim(now)) return false;
            _claimant = dog;
            return true;
        }

        public bool IsClaimedBy(object dog) => dog != null && ReferenceEquals(_claimant, dog);

        public bool TryStartEating(object dog, float now)
        {
            if (!IsClaimedBy(dog) || _eatingUntil > 0f) return false;
            _eatingUntil = now + MeatDistractionSource.EatingSeconds;
            return true;
        }

        public bool IsEating(object dog, float now) => IsClaimedBy(dog) && _eatingUntil > now;

        public bool IsExpired(float now) => _landed &&
            (_claimant == null ? now >= _availableUntil : _eatingUntil > 0f && now >= _eatingUntil);

        public void ReleaseClaim(object dog)
        {
            if (!IsClaimedBy(dog)) return;
            _claimant = null;
            _eatingUntil = 0f;
            // Availability remains anchored to landing, so resetting a dog cannot extend the food window.
        }
    }
}
