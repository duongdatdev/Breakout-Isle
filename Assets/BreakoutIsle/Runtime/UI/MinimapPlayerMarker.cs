using UnityEngine;

namespace BreakoutIsle.UI
{
    public sealed class MinimapPlayerMarker : MonoBehaviour
    {
        [SerializeField] private RectTransform mapContent;
        [SerializeField] private RectTransform marker;
        [SerializeField] private Transform player;
        [SerializeField] private Rect worldBounds = new Rect(2f, 2f, 36f, 20f);

        public void Bind(RectTransform content, RectTransform playerMarker, Transform target, Rect bounds)
        {
            mapContent = content;
            marker = playerMarker;
            player = target;
            worldBounds = bounds;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (mapContent == null || marker == null || player == null || worldBounds.width <= 0f || worldBounds.height <= 0f) return;
            var position = (Vector2)player.position;
            var x = Mathf.Clamp01((position.x - worldBounds.xMin) / worldBounds.width);
            var y = Mathf.Clamp01((position.y - worldBounds.yMin) / worldBounds.height);
            marker.anchoredPosition = new Vector2(x * mapContent.rect.width, y * mapContent.rect.height);
        }
    }
}
