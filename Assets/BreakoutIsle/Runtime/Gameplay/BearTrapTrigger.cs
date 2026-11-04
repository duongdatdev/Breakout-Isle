using UnityEngine;

namespace BreakoutIsle.Gameplay
{
public sealed class BearTrapTrigger : MonoBehaviour
    {
        [SerializeField] private BearTrapHazard hazard;
        [SerializeField] private Collider2D trapCollider;

        private void Awake()
        {
            if (trapCollider == null) trapCollider = GetComponent<Collider2D>();
            if (trapCollider != null) trapCollider.isTrigger = true;
            if (hazard == null) hazard = GetComponentInParent<BearTrapHazard>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hazard != null) hazard.Activate(other);
        }
    }
}
