using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class PlayerStealthZone : MonoBehaviour
    {
        private int _darkZoneCount;
        public bool IsInDarkZone => _darkZoneCount > 0;
        private void OnTriggerEnter2D(Collider2D other) { if (other.GetComponent<DarkZone>() != null) _darkZoneCount++; }
        private void OnTriggerExit2D(Collider2D other) { if (other.GetComponent<DarkZone>() != null) _darkZoneCount = Mathf.Max(0, _darkZoneCount - 1); }
    }
}
