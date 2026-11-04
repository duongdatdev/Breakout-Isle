using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class PowerSwitchVisual : MonoBehaviour, IPowerConsumer
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite poweredSprite;
        [SerializeField] private Sprite unpoweredSprite;

        public bool IsPowered { get; private set; }

        public void SetPowered(bool powered)
        {
            IsPowered = powered;
            if (spriteRenderer != null)
                spriteRenderer.sprite = powered ? poweredSprite : unpoweredSprite;
        }
    }
}
