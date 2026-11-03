using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [RequireComponent(typeof(Camera))]
    public sealed class PixelCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(1)] private int pixelsPerUnit = 32;
        [SerializeField] private float verticalResolution = 360f;

        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = verticalResolution / (2f * pixelsPerUnit);
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            var position = SnapToPixelGrid(target.position, pixelsPerUnit);
            position.z = transform.position.z;
            transform.position = position;
        }

        public static Vector3 SnapToPixelGrid(Vector3 position, int pixelsPerUnit)
        {
            var unit = 1f / Mathf.Max(1, pixelsPerUnit);
            position.x = Mathf.Round(position.x / unit) * unit;
            position.y = Mathf.Round(position.y / unit) * unit;
            return position;
        }
    }
}
