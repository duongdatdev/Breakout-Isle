using UnityEngine;

namespace BreakoutIsle.Gameplay
{
public sealed class BearTrapRevealSensor : MonoBehaviour
    {
        [SerializeField] private BearTrapHazard hazard;
        [SerializeField, Min(0.1f)] private float revealRadius = 2f;
        [SerializeField] private Collider2D sensor;

        private void Awake()
        {
            if (sensor == null) sensor = GetComponent<Collider2D>();
            if (sensor != null) sensor.isTrigger = true;
            if (hazard == null) hazard = GetComponentInParent<BearTrapHazard>();
        }

        private void OnTriggerEnter2D(Collider2D other) => RevealForPlayer(other);
        private void OnTriggerStay2D(Collider2D other) => RevealForPlayer(other);

        private void RevealForPlayer(Collider2D other)
        {
            if (hazard == null || other.GetComponentInParent<PlayerMotor>() == null) return;
            if (Vector2.Distance(transform.position, other.transform.position) <= revealRadius)
                hazard.Reveal();
        }
    }
}
