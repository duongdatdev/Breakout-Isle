using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class LightPowerConsumer : MonoBehaviour, IPowerConsumer
    {
        [SerializeField] private Behaviour targetLight;

        public bool IsPowered { get; private set; }

        public void SetPowered(bool powered)
        {
            IsPowered = powered;
            if (targetLight != null) targetLight.enabled = powered;
        }
    }
}
