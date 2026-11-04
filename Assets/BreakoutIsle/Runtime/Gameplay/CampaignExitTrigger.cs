using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class CampaignExitTrigger : MonoBehaviour
    {
        [SerializeField] private CampaignTransitionService transitionService;

        private void Awake()
        {
            if (transitionService == null) transitionService = FindObjectOfType<CampaignTransitionService>();
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (transitionService == null || other.GetComponentInParent<PlayerInteractor>() == null) return;
            transitionService.TryAdvance();
        }
    }
}
