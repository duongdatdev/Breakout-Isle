using System;
using UnityEngine;

namespace BreakoutIsle.Audio
{
    [Serializable]
    public sealed class AudioEventDefinition
    {
        public string id;
        public AudioClip[] clips;
    }

    [CreateAssetMenu(menuName = "Breakout Isle/Audio Catalog", fileName = "AudioCatalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [SerializeField] private AudioEventDefinition[] events = Array.Empty<AudioEventDefinition>();
        public AudioEventDefinition[] Events => events;
        public void SetEvents(AudioEventDefinition[] value) => events = value ?? Array.Empty<AudioEventDefinition>();

        public bool TryGet(string id, out AudioClip[] clips)
        {
            if (events != null)
                foreach (var entry in events)
                    if (entry != null && string.Equals(entry.id, id, StringComparison.Ordinal))
                    {
                        clips = entry.clips ?? Array.Empty<AudioClip>();
                        return clips.Length > 0;
                    }
            clips = Array.Empty<AudioClip>();
            return false;
        }
    }
}
