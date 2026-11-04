using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    /// <summary>Shows an authored chest visual/interaction root while its linked power is off.</summary>
    public sealed class ChestPowerReveal : MonoBehaviour, IPowerConsumer
    {
        [SerializeField] private GameObject[] objectsRevealedWhenUnpowered;

        public bool IsPowered { get; private set; }

        public void SetPowered(bool powered)
        {
            IsPowered = powered;
            if (objectsRevealedWhenUnpowered == null) return;
            foreach (var target in objectsRevealedWhenUnpowered)
                if (target != null) target.SetActive(!powered);
        }
    }
}
