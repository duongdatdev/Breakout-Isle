using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BreakoutIsle.Audio
{
    public sealed class AudioService : MonoBehaviour
    {
        private const string MasterVolumeKey = "breakout.audio.master";
        private const string MutedKey = "breakout.audio.muted";
        public static AudioService Instance { get; private set; }

        [SerializeField] private AudioCatalog catalog;
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource musicA;
        [SerializeField] private AudioSource musicB;
        [SerializeField, Min(0.05f)] private float musicFadeSeconds = 1.2f;
        private AudioSource _activeMusic;
        private Coroutine _musicFade;
        private float _masterVolume = 1f;
        private bool _muted;
        private int _lastClipIndex = -1;
        private float _musicGainA;
        private float _musicGainB;
        private readonly List<AudioSource> _spatialOneShots = new List<AudioSource>();

        public float MasterVolume => _masterVolume;
        public bool Muted => _muted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSources();
            _masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            _muted = PlayerPrefs.GetInt(MutedKey, 0) != 0;
            ApplyVolume();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit() => PlayerPrefs.Save();

        public void Configure(AudioCatalog value) => catalog = value;

        public bool Play(string eventId, float volume = 1f)
        {
            if (oneShotSource == null || !TrySelect(eventId, out var clip)) return false;
            oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            return true;
        }

        public bool PlayAt(string eventId, Vector3 position, float volume = 1f)
        {
            if (catalog == null || !catalog.TryGet(eventId, out var clips) || clips.Length == 0) return false;
            var clip = Select(clips);
            if (clip == null) return false;
            var source = FindAvailableSpatialSource();
            source.transform.position = position;
            source.volume = EffectiveVolume;
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
            return true;
        }

        public bool PlayLoop(string eventId, bool ambience = true)
        {
            if (catalog == null || !catalog.TryGet(eventId, out var clips) || clips.Length == 0) return false;
            var source = ambience ? ambienceSource : musicA;
            if (source == null) return false;
            source.clip = Select(clips);
            source.loop = true;
            source.volume = EffectiveVolume;
            source.Play();
            if (!ambience)
            {
                _activeMusic = source;
                SetMusicGain(source, 1f);
                var other = source == musicA ? musicB : musicA;
                if (other != null)
                {
                    other.Stop();
                    SetMusicGain(other, 0f);
                }
            }
            return true;
        }

        public bool CrossfadeMusic(string eventId)
        {
            if (catalog == null || !catalog.TryGet(eventId, out var clips) || clips.Length == 0) return false;
            var target = _activeMusic == musicA ? musicB : musicA;
            if (target == null) return false;
            var source = _activeMusic;
            var selectedClip = Select(clips);
            if (source != null && source.isPlaying && source.clip == selectedClip) return true;
            if (_musicFade != null)
            {
                StopCoroutine(_musicFade);
                _musicFade = null;
            }
            target.Stop();
            target.clip = selectedClip;
            target.loop = true;
            SetMusicGain(target, 0f);
            target.Play();
            // Mark the new target immediately. If another state arrives mid-fade, the next
            // transition uses the currently audible target instead of restarting it.
            _activeMusic = target;
            _musicFade = StartCoroutine(FadeMusic(source, target));
            return true;
        }

        public void SetChaseActive(bool active) => CrossfadeMusic(active ? "music.chase" : "music.stealth");

        public void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
            ApplyVolume();
        }

        public void SetMuted(bool value)
        {
            _muted = value;
            PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
            ApplyVolume();
        }

        private float EffectiveVolume => _muted ? 0f : _masterVolume;

        private IEnumerator FadeMusic(AudioSource from, AudioSource to)
        {
            var fromStart = GetMusicGain(from);
            var duration = Mathf.Max(0.05f, musicFadeSeconds);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                SetMusicGain(to, t);
                if (from != null) SetMusicGain(from, fromStart * (1f - t));
                yield return null;
            }
            SetMusicGain(to, 1f);
            if (from != null)
            {
                from.Stop();
                SetMusicGain(from, 0f);
            }
            _activeMusic = to;
            _musicFade = null;
        }

        private bool TrySelect(string eventId, out AudioClip clip)
        {
            clip = null;
            if (catalog == null || !catalog.TryGet(eventId, out var clips) || clips.Length == 0) return false;
            clip = Select(clips);
            return clip != null;
        }

        private AudioClip Select(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];
            var index = Random.Range(0, clips.Length);
            if (index == _lastClipIndex) index = (index + 1) % clips.Length;
            _lastClipIndex = index;
            return clips[index];
        }

        private void EnsureSources()
        {
            if (oneShotSource == null) oneShotSource = CreateSource("One Shots", false);
            if (ambienceSource == null) ambienceSource = CreateSource("Ambience", true);
            if (musicA == null) musicA = CreateSource("Music A", true);
            if (musicB == null) musicB = CreateSource("Music B", true);
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private void ApplyVolume()
        {
            AudioListener.volume = 1f;
            if (oneShotSource != null) oneShotSource.volume = EffectiveVolume;
            if (ambienceSource != null) ambienceSource.volume = EffectiveVolume;
            foreach (var source in _spatialOneShots)
                if (source != null) source.volume = EffectiveVolume;
            if (musicA != null) musicA.volume = _musicGainA * EffectiveVolume;
            if (musicB != null) musicB.volume = _musicGainB * EffectiveVolume;
        }

        private AudioSource FindAvailableSpatialSource()
        {
            foreach (var source in _spatialOneShots)
                if (source != null && !source.isPlaying) return source;

            var sourceObject = new GameObject("Spatial One Shot " + _spatialOneShots.Count);
            sourceObject.transform.SetParent(transform, false);
            var created = sourceObject.AddComponent<AudioSource>();
            created.playOnAwake = false;
            created.loop = false;
            created.spatialBlend = 1f;
            created.volume = EffectiveVolume;
            _spatialOneShots.Add(created);
            return created;
        }

        private float GetMusicGain(AudioSource source)
        {
            if (source == musicA) return _musicGainA;
            if (source == musicB) return _musicGainB;
            return 0f;
        }

        private void SetMusicGain(AudioSource source, float gain)
        {
            if (source == null) return;
            gain = Mathf.Clamp01(gain);
            if (source == musicA) _musicGainA = gain;
            else if (source == musicB) _musicGainB = gain;
            source.volume = gain * EffectiveVolume;
        }
    }
}
