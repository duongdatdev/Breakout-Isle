using UnityEngine;
using UnityEngine.Rendering;

namespace BreakoutIsle.Gameplay
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SecurityCameraCone : MonoBehaviour
    {
        private const int RayCount = 36;
        [SerializeField] private SecurityCamera securityCamera;
        [SerializeField] private LayerMask obstacleMask;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Material _material;
        private float _refreshTimer;

        public void Configure(SecurityCamera camera, LayerMask obstacles)
        {
            securityCamera = camera;
            obstacleMask = obstacles;
        }

        private void Awake()
        {
            if (securityCamera == null) securityCamera = GetComponentInParent<SecurityCamera>();
            _mesh = new Mesh { name = "Wall-clipped security camera view" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = GetComponent<MeshRenderer>();
            _renderer.sortingLayerName = "Actors";
            _renderer.sortingOrder = -75;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _material = new Material(shader) { name = "Security camera view tint" };
                _renderer.sharedMaterial = _material;
            }
        }

        private void LateUpdate()
        {
            if (securityCamera == null || !securityCamera.IsPowered)
            {
                if (_renderer != null) _renderer.enabled = false;
                return;
            }
            _renderer.enabled = true;
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 0.1f;
            RebuildCone();
        }

        private void RebuildCone()
        {
            var localForward = securityCamera.LocalForward;
            var range = securityCamera.DetectionRange;
            var angle = securityCamera.FieldOfViewDegrees;
            var startAngle = Mathf.Atan2(localForward.y, localForward.x) * Mathf.Rad2Deg - angle * 0.5f;
            var vertices = new Vector3[RayCount + 2];
            var triangles = new int[RayCount * 3];
            vertices[0] = Vector3.zero;
            for (var i = 0; i <= RayCount; i++)
            {
                var degrees = startAngle + angle * i / RayCount;
                var radians = degrees * Mathf.Deg2Rad;
                var localDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                var worldDirection = (Vector2)transform.TransformDirection(localDirection);
                var distance = range;
                foreach (var hit in Physics2D.RaycastAll(transform.position, worldDirection, range, obstacleMask))
                    if (hit.collider != null && !hit.collider.isTrigger && hit.distance < distance)
                        distance = hit.distance;
                vertices[i + 1] = localDirection * distance;
                if (i < RayCount)
                {
                    var index = i * 3;
                    triangles[index] = 0;
                    triangles[index + 1] = i + 1;
                    triangles[index + 2] = i + 2;
                }
            }
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            if (_material != null) _material.color = new Color(0.9f, 0.62f, 0.16f, 0.18f);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
