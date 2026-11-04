using System;
using System.Collections;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class AnimatedQuestChest : MonoBehaviour, IPlayerInteractable, IPersistedWorldState
    {
        [SerializeField] private string persistentId;
        [SerializeField] private string objectiveId;
        [SerializeField] private string prompt = "Open chest";
        [SerializeField] private LevelSession session;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] openFrames;
        [SerializeField, Min(1)] private int framesPerSecond = 8;

        private bool _opened;

        public string StableId => persistentId;
        public string PersistentId => persistentId;
        public string Prompt => prompt;
        public bool IsOpened => _opened;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (session == null) session = FindAnyObjectByType<LevelSession>();
        }

        public bool CanInteract(PlayerInteractor player) => !_opened && player != null && player.Session != null;

        public void Interact(PlayerInteractor player)
        {
            if (!CanInteract(player) || !player.Session.CompleteObjective(objectiveId)) return;
            Open();
        }

        public WorldEntityRecord CaptureWorldState() => new WorldEntityRecord
        {
            stableId = persistentId,
            state = _opened ? "open" : "closed"
        };

        public WorldEntityRecord CaptureBaselineWorldState() => new WorldEntityRecord
        {
            stableId = persistentId,
            state = "closed"
        };

        public void RestoreWorldState(WorldEntityRecord record)
        {
            if (record == null || record.stableId != persistentId ||
                (record.state != "open" && record.state != "closed"))
                throw new ArgumentException("Chest state does not match this stable ID.", nameof(record));
            StopAllCoroutines();
            _opened = record.state == "open";
            SetFrame(_opened && openFrames != null && openFrames.Length > 0 ? openFrames[openFrames.Length - 1] :
                openFrames != null && openFrames.Length > 0 ? openFrames[0] : null);
        }

        private void Open()
        {
            if (_opened) return;
            _opened = true;
            if (openFrames == null || openFrames.Length == 0)
            {
                Debug.LogError("AnimatedQuestChest needs generated chest frames.", this);
                return;
            }
            if (Application.isPlaying)
                StartCoroutine(PlayOpenAnimation());
            else
                SetFrame(openFrames[openFrames.Length - 1]);
        }

        private IEnumerator PlayOpenAnimation()
        {
            foreach (var frame in openFrames)
            {
                SetFrame(frame);
                yield return new WaitForSeconds(1f / framesPerSecond);
            }
        }

        private void SetFrame(Sprite sprite)
        {
            if (spriteRenderer != null && sprite != null) spriteRenderer.sprite = sprite;
        }
    }
}
