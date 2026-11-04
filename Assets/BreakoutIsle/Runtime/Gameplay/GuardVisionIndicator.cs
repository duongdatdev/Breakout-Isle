using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GuardVisionIndicator : MonoBehaviour
    {
        private const int RayCount = 48;
        private Mesh _mesh;
        private MeshRenderer _meshRenderer;
        private Material _material;
        private float _refreshTimer;
        private LayerMask _obstacles;
        private float _range;
        private float _angle;
        private Vector2 _forward;
        private GuardState _state;
        private float _detection;
        private TextMesh _stateText;
        private Transform _fillTransform;
        private SpriteRenderer _barBackground;
        private SpriteRenderer _barFill;
        private static Sprite _whiteSprite;

        private void Awake()
        {
            _mesh = new Mesh { name = "Clipped guard vision" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.sortingLayerName = "Actors";
            _meshRenderer.sortingOrder = -80;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _material = new Material(shader) { name = "Guard vision tint" };
                _meshRenderer.sharedMaterial = _material;
            }
            CreateStatusWidgets();
        }

        private void LateUpdate()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 0.1f;
            RebuildCone();
            UpdateStatusWidgets();
        }

        public void SetView(Vector2 forward, float range, float angle, GuardState state, float detection)
        {
            _forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.down;
            _range = range;
            _angle = angle;
            _state = state;
            _detection = Mathf.Clamp01(detection);
        }

        public void SetObstacleMask(LayerMask mask) => _obstacles = mask;

        private void RebuildCone()
        {
            if (_mesh == null || _range <= 0f) return;
            var vertices = new Vector3[RayCount + 2];
            var triangles = new int[RayCount * 3];
            var startAngle = Mathf.Atan2(_forward.y, _forward.x) * Mathf.Rad2Deg - _angle * 0.5f;
            for (var i = 0; i <= RayCount; i++)
            {
                var degrees = startAngle + _angle * i / RayCount;
                var radians = degrees * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                var hit = Physics2D.Raycast(transform.position, direction, _range, _obstacles);
                var distance = hit.collider != null ? hit.distance : _range;
                vertices[i + 1] = direction * distance;
                if (i == RayCount) continue;
                var triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = i + 2;
            }
            vertices[0] = Vector3.zero;
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            if (_material != null) _material.color = StateColor(_state);
        }

        private void CreateStatusWidgets()
        {
            var stateObject = new GameObject("Guard State Marker");
            stateObject.transform.SetParent(transform, false);
            stateObject.transform.localPosition = new Vector3(0f, 1.12f, 0f);
            _stateText = stateObject.AddComponent<TextMesh>();
            _stateText.anchor = TextAnchor.MiddleCenter;
            _stateText.alignment = TextAlignment.Center;
            _stateText.characterSize = 0.045f;
            _stateText.fontSize = 48;
            _stateText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _stateText.GetComponent<MeshRenderer>().sortingLayerName = "Actors";
            _stateText.GetComponent<MeshRenderer>().sortingOrder = 54;

            EnsureWhiteSprite();
            var backObject = new GameObject("Guard Detection Back");
            backObject.transform.SetParent(transform, false);
            backObject.transform.localPosition = new Vector3(0f, 0.88f, 0f);
            backObject.transform.localScale = new Vector3(0.8f, 0.09f, 1f);
            _barBackground = backObject.AddComponent<SpriteRenderer>();
            _barBackground.sprite = _whiteSprite;
            _barBackground.color = new Color(0.07f, 0.07f, 0.07f, 0.9f);
            _barBackground.sortingLayerName = "Actors";
            _barBackground.sortingOrder = 52;

            var fillObject = new GameObject("Guard Detection Fill");
            fillObject.transform.SetParent(backObject.transform, false);
            fillObject.transform.localPosition = new Vector3(-0.5f, 0f, -0.01f);
            _fillTransform = fillObject.transform;
            _barFill = fillObject.AddComponent<SpriteRenderer>();
            _barFill.sprite = _whiteSprite;
            _barFill.color = new Color(0.9f, 0.8f, 0.25f, 1f);
            _barFill.sortingLayerName = "Actors";
            _barFill.sortingOrder = 53;
        }

        private void UpdateStatusWidgets()
        {
            if (_stateText == null) return;
            _stateText.text = _state == GuardState.Chase ? "!" :
                _state == GuardState.Suspicious || _state == GuardState.Search || _state == GuardState.Alert ? "?" : string.Empty;
            _stateText.color = _state == GuardState.Chase || _state == GuardState.Alert
                ? new Color(1f, 0.25f, 0.2f, 1f)
                : new Color(1f, 0.82f, 0.2f, 1f);
            var visible = _detection > 0.005f || _state == GuardState.Suspicious || _state == GuardState.Chase;
            _barBackground.enabled = visible;
            _barFill.enabled = visible;
            var width = Mathf.Max(0.008f, _detection);
            _fillTransform.localScale = new Vector3(width, 1f, 1f);
            _fillTransform.localPosition = new Vector3((width - 1f) * 0.5f, 0f, -0.01f);
            _barFill.color = _state == GuardState.Chase ? new Color(1f, 0.18f, 0.15f, 1f) : new Color(1f, 0.78f, 0.2f, 1f);
        }

        private static Color StateColor(GuardState state)
        {
            if (state == GuardState.Chase) return new Color(1f, 0.14f, 0.11f, 0.19f);
            if (state == GuardState.Suspicious) return new Color(1f, 0.78f, 0.18f, 0.16f);
            if (state == GuardState.Search || state == GuardState.Alert) return new Color(0.96f, 0.58f, 0.2f, 0.14f);
            return new Color(0.8f, 0.9f, 0.94f, 0.1f);
        }

        private static void EnsureWhiteSprite()
        {
            if (_whiteSprite == null)
                _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
