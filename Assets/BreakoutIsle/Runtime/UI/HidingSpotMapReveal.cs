using BreakoutIsle.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace BreakoutIsle.UI
{
    public sealed class HidingSpotMapReveal : MonoBehaviour
    {
        [SerializeField] private HidingSpot hidingSpot;
        [SerializeField] private Transform player;
        [SerializeField, Min(0.1f)] private float revealDistance = 3f;
        private Image _marker;
        private bool _discovered;

        public void Bind(HidingSpot spot, Transform playerTransform, float radius = 3f)
        {
            hidingSpot = spot;
            player = playerTransform;
            revealDistance = radius;
            _marker = GetComponent<Image>();
            if (_marker != null) _marker.enabled = false;
        }

        private void Awake() => _marker = GetComponent<Image>();

        private void Update()
        {
            if (_discovered || hidingSpot == null || player == null) return;
            if (Vector2.Distance(player.position, hidingSpot.transform.position) <= revealDistance)
            {
                _discovered = true;
                if (_marker != null) _marker.enabled = true;
            }
        }
    }
}
