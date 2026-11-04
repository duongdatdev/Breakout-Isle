using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BreakoutIsle.Gameplay
{
    public sealed class SupportItemController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private LevelSession session;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SupportProjectile projectilePrefab;
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private GameObject landingMarker;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField, Min(0.1f)] private float maximumRange = 6f;
        [SerializeField, Min(0.05f)] private float flightDuration = 0.6f;
        [SerializeField, Min(0f)] private float previewArcHeight = 0.65f;
        [SerializeField, Min(2)] private int previewSegments = 16;
        [SerializeField] private SupportItemProfile[] profiles;

        private PlayerStealthState _stealthState;
        private bool _aiming;
        private int _aimSlot = -1;
        private SupportItemProfile _aimProfile;

        public event Action<Vector2> ProjectileLaunched;

        private void Awake()
        {
            if (inputReader == null) inputReader = GetComponent<PlayerInputReader>();
            if (session == null) session = FindObjectOfType<LevelSession>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (worldCamera == null) worldCamera = Camera.main;
            _stealthState = GetComponent<PlayerStealthState>();
            if (aimLine != null) aimLine.positionCount = previewSegments;
            ShowAim(false);
        }

        private void OnEnable()
        {
            if (inputReader == null) return;
            inputReader.ThrowPressed += BeginAim;
            inputReader.ThrowReleased += ReleaseAim;
            inputReader.PausePressed += CancelAim;
            inputReader.ObjectivesPressed += CancelAim;
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.ThrowPressed -= BeginAim;
                inputReader.ThrowReleased -= ReleaseAim;
                inputReader.PausePressed -= CancelAim;
                inputReader.ObjectivesPressed -= CancelAim;
            }
            CancelAim();
        }

        private void Update()
        {
            if (!_aiming) return;
            if (_stealthState != null && _stealthState.IsCaptured)
            {
                CancelAim();
                return;
            }
            if (!IsAimSlotStillValid())
            {
                CancelAim();
                return;
            }
            if (!TryGetAim(out var start, out var destination))
            {
                ShowAim(false);
                return;
            }
            ShowAim(true);
            DrawAim(start, destination);
        }

        private void BeginAim()
        {
            if (session == null || session.Inventory == null ||
                (_stealthState != null && _stealthState.IsCaptured)) return;
            var slot = session.SelectedSupportSlot;
            var items = session.Inventory.SupportItems;
            if (slot < 0 || slot >= items.Count || string.IsNullOrWhiteSpace(items[slot])) return;
            var itemId = items[slot];
            if (itemId == "energy_drink")
            {
                UseEnergyDrink(slot);
                return;
            }
            if (!TryGetProfile(itemId, out var profile) || projectilePrefab == null)
            {
                session.ShowMessage("Vật phẩm này chưa thể ném.");
                return;
            }
            _aiming = true;
            _aimSlot = slot;
            _aimProfile = profile;
        }

        private void ReleaseAim()
        {
            if (!_aiming) return;
            if (!IsAimSlotStillValid() || projectilePrefab == null || !TryGetAim(out var start, out var destination))
            {
                CancelAim();
                return;
            }

            var projectile = Instantiate(projectilePrefab, start, Quaternion.identity);
            if (!projectile.Launch(_aimProfile.itemId, start, destination, _aimProfile, obstacleMask, flightDuration))
            {
                Destroy(projectile.gameObject);
                CancelAim();
                return;
            }
            if (!session.TryConsumeSupportItemAt(_aimSlot, out var consumedItemId) || consumedItemId != _aimProfile.itemId)
            {
                Destroy(projectile.gameObject);
                session.ShowMessage("Không thể dùng vật phẩm đã chọn.");
                CancelAim();
                return;
            }
            ProjectileLaunched?.Invoke((destination - start).normalized);
            CancelAim();
        }

        private void UseEnergyDrink(int slot)
        {
            if (motor == null)
            {
                session.ShowMessage("Không thể dùng nước tăng lực lúc này.");
                return;
            }
            if (!session.TryConsumeSupportItemAt(slot, out var consumedItemId) || consumedItemId != "energy_drink")
            {
                session.ShowMessage("Không thể dùng vật phẩm đã chọn.");
                return;
            }
            motor.ApplyEnergyDrink();
        }

        private bool IsAimSlotStillValid()
        {
            if (_aimSlot < 0 || session == null || session.Inventory == null) return false;
            var items = session.Inventory.SupportItems;
            return _aimSlot < items.Count && items[_aimSlot] == _aimProfile.itemId;
        }

        private bool TryGetAim(out Vector2 start, out Vector2 destination)
        {
            start = transform.position;
            destination = start;
            if (worldCamera == null || Mouse.current == null) return false;
            var screenPosition = Mouse.current.position.ReadValue();
            var worldPosition = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y,
                -worldCamera.transform.position.z));
            var offset = (Vector2)worldPosition - start;
            var distance = offset.magnitude;
            if (distance <= 0.05f) return false;
            var direction = offset / distance;
            var clippedDistance = Mathf.Min(distance, maximumRange);
            if (obstacleMask.value != 0)
            {
                var wall = Physics2D.Raycast(start, direction, clippedDistance, obstacleMask);
                if (wall.collider != null)
                    clippedDistance = Mathf.Max(0f, wall.distance - 0.06f);
            }
            if (clippedDistance <= 0.05f) return false;
            destination = start + direction * clippedDistance;
            return true;
        }

        private void DrawAim(Vector2 start, Vector2 destination)
        {
            if (aimLine != null)
            {
                var count = Mathf.Max(2, previewSegments);
                if (aimLine.positionCount != count) aimLine.positionCount = count;
                for (var i = 0; i < count; i++)
                {
                    var t = i / (float)(count - 1);
                    var point = Vector2.Lerp(start, destination, t) + Vector2.up * (previewArcHeight * 4f * t * (1f - t));
                    aimLine.SetPosition(i, point);
                }
            }
            if (landingMarker != null)
                landingMarker.transform.position = destination;
        }

        private bool TryGetProfile(string itemId, out SupportItemProfile profile)
        {
            if (profiles != null)
                foreach (var candidate in profiles)
                    if (candidate != null && candidate.itemId == itemId)
                    {
                        profile = candidate;
                        return true;
                    }

            profile = itemId switch
            {
                "stone" => new SupportItemProfile { itemId = itemId, noisePolicy = SupportNoisePolicy.NearestGuard, noiseRadius = 6f },
                "tin_can" => new SupportItemProfile { itemId = itemId, noisePolicy = SupportNoisePolicy.NearestGuard, noiseRadius = 6f },
                "glass_bottle" => new SupportItemProfile { itemId = itemId, noisePolicy = SupportNoisePolicy.AllGuards, noiseRadius = 10f },
                "firecracker" => new SupportItemProfile { itemId = itemId, noisePolicy = SupportNoisePolicy.AllGuards, noiseRadius = 12f, fuseSeconds = 3f },
                "meat" => new SupportItemProfile { itemId = itemId, attractsDogs = true },
                _ => null
            };
            return profile != null;
        }

        private void ShowAim(bool visible)
        {
            if (aimLine != null) aimLine.enabled = visible;
            if (landingMarker != null) landingMarker.SetActive(visible);
        }

        private void CancelAim()
        {
            _aiming = false;
            _aimSlot = -1;
            _aimProfile = null;
            ShowAim(false);
        }
    }
}
