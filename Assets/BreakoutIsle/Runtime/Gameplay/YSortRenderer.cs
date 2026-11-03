using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [RequireComponent(typeof(Renderer))]
    public sealed class YSortRenderer : MonoBehaviour
    {
        [SerializeField] private int orderOffset;
        [SerializeField, Min(1)] private int precision = 100;

        private Renderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
        }

        private void LateUpdate()
        {
            _renderer.sortingOrder = SortingOrderForY(transform.position.y, precision, orderOffset);
        }

        public static int SortingOrderForY(float worldY, int precision, int orderOffset = 0)
        {
            return orderOffset - Mathf.RoundToInt(worldY * Mathf.Max(1, precision));
        }
    }
}
