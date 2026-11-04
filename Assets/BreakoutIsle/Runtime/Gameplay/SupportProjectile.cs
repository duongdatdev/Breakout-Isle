using System.Collections;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class SupportProjectile : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, Min(0f)] private float arcHeight = 0.65f;

        private Vector2 _groundPosition;
        private Vector2 _startPosition;
        private Vector2 _landingPosition;
        private Vector2 _previousGroundPosition;
        private LayerMask _obstacleMask;
        private SupportItemProfile _profile;
        private float _flightDuration;
        private float _launchTime;
        private bool _launched;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public bool Launch(string itemId, Vector2 start, Vector2 destination, SupportItemProfile profile,
            LayerMask obstacleMask, float flightDuration)
        {
            if (_launched || profile == null || profile.itemId != itemId || flightDuration <= 0f ||
                profile.attractsDogs && profile.projectileSprite == null) return false;
            _launched = true;
            _startPosition = start;
            _groundPosition = start;
            _previousGroundPosition = start;
            _landingPosition = destination;
            _profile = profile;
            _obstacleMask = obstacleMask;
            _flightDuration = flightDuration;
            _launchTime = Time.time;
            if (spriteRenderer != null && profile.projectileSprite != null)
                spriteRenderer.sprite = profile.projectileSprite;
            transform.position = start;
            StartCoroutine(Fly());
            return true;
        }

        private IEnumerator Fly()
        {
            var elapsed = 0f;
            while (elapsed < _flightDuration)
            {
                elapsed = Mathf.Min(_flightDuration, elapsed + Time.deltaTime);
                var t = elapsed / _flightDuration;
                var nextGroundPosition = Vector2.Lerp(_startPosition, _landingPosition, t);
                if (_obstacleMask.value != 0)
                {
                    var wall = Physics2D.Linecast(_previousGroundPosition, nextGroundPosition, _obstacleMask);
                    if (wall.collider != null)
                    {
                        _groundPosition = wall.point;
                        transform.position = _groundPosition;
                        yield return FinishAtLanding();
                        yield break;
                    }
                }

                _groundPosition = nextGroundPosition;
                _previousGroundPosition = nextGroundPosition;
                transform.position = _groundPosition + Vector2.up * (arcHeight * 4f * t * (1f - t));
                yield return null;
            }

            _groundPosition = _landingPosition;
            transform.position = _groundPosition;
            yield return FinishAtLanding();
        }

        private IEnumerator FinishAtLanding()
        {
            var fuseRemaining = _profile.fuseSeconds - (Time.time - _launchTime);
            if (fuseRemaining > 0f)
                yield return new WaitForSeconds(fuseRemaining);
            if (_profile.attractsDogs)
            {
                var source = GetComponent<MeatDistractionSource>();
                if (source == null) source = gameObject.AddComponent<MeatDistractionSource>();
                source.Land(_groundPosition, _profile.projectileSprite, Time.time);
                yield break;
            }
            switch (_profile.noisePolicy)
            {
                case SupportNoisePolicy.NearestGuard:
                    NoiseSystem.EmitNearest(_groundPosition, _profile.noiseRadius);
                    break;
                case SupportNoisePolicy.AllGuards:
                    NoiseSystem.Emit(_groundPosition, _profile.noiseRadius);
                    break;
            }
            Destroy(gameObject);
        }
    }
}
