using System.Reflection;
using BreakoutIsle.Audio;
using BreakoutIsle.Gameplay;
using BreakoutIsle.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BreakoutIsle.Tests
{
    public sealed class UIAudioIntegrationTests
    {
        [Test]
        public void LevelMessageHudDisplaysSessionMessages()
        {
            var sessionObject = new GameObject("Session");
            var session = sessionObject.AddComponent<LevelSession>();
            var hudObject = new GameObject("Message HUD");
            hudObject.SetActive(false);
            var labelObject = new GameObject("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(hudObject.transform, false);
            var label = labelObject.GetComponent<Text>();
            var hud = hudObject.AddComponent<LevelMessageHud>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("session").objectReferenceValue = session;
            serialized.FindProperty("messageText").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            hudObject.SetActive(true);
            hud.Bind(session, label);
            session.ShowMessage("Tương tác thất bại");

            Assert.That(label.text, Is.EqualTo("Tương tác thất bại"));
            Object.DestroyImmediate(hudObject);
            Object.DestroyImmediate(sessionObject);
        }

        [Test]
        public void AudioCatalogReturnsClipsByStableEventId()
        {
            var clip = AudioClip.Create("catalog_test", 128, 1, 44100, false);
            var catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            catalog.SetEvents(new[] { new AudioEventDefinition { id = "ui.confirm", clips = new[] { clip } } });

            Assert.That(catalog.TryGet("ui.confirm", out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(new[] { clip }));
            Assert.That(catalog.TryGet("audio.missing", out var missing), Is.False);
            Assert.That(missing, Is.Empty);

            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void MasterAndMuteAffectOneShotsAndMusicWithoutBlockingPlaybackRequests()
        {
            var clip = AudioClip.Create("audio_mix_test", 256, 1, 44100, false);
            var catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            catalog.SetEvents(new[]
            {
                new AudioEventDefinition { id = "sfx.test", clips = new[] { clip } },
                new AudioEventDefinition { id = "music.test", clips = new[] { clip } }
            });
            var serviceObject = new GameObject("Audio Service Test");
            var service = serviceObject.AddComponent<AudioService>();
            service.Configure(catalog);
            InvokePrivate(service, "EnsureSources");
            service.SetMuted(false);
            service.SetMasterVolume(0.6f);

            try
            {
                Assert.That(service.Play("sfx.test"), Is.True);
                var oneShot = ReadAudioSource(service, "oneShotSource");
                Assert.That(oneShot.volume, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(service.PlayLoop("music.test", false), Is.True);
                var music = ReadAudioSource(service, "musicA");
                Assert.That(music.volume, Is.EqualTo(0.6f).Within(0.001f));

                service.SetMasterVolume(0.25f);
                Assert.That(oneShot.volume, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(music.volume, Is.EqualTo(0.25f).Within(0.001f));
                service.SetMuted(true);
                Assert.That(service.Play("sfx.test"), Is.True, "Mute should silence output without suppressing event logic.");
                Assert.That(oneShot.volume, Is.Zero);
                Assert.That(music.volume, Is.Zero);
                service.SetMuted(false);
                Assert.That(oneShot.volume, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(music.volume, Is.EqualTo(0.25f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void ResumeUnblocksPlayerInput()
        {
            var fixture = CreateHudFixture();
            try
            {
                fixture.hud.TogglePause();
                Assert.That(ReadGameplayBlocked(fixture.input), Is.True);
                fixture.hud.Resume();
                Assert.That(ReadGameplayBlocked(fixture.input), Is.False);
                Assert.That(fixture.pause.activeSelf, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally { DestroyFixture(fixture); }
        }

        [Test]
        public void FailedRetryKeepsCaughtScreenAndGameplayBlocked()
        {
            var fixture = CreateHudFixture(true);
            try
            {
                fixture.hud.SetRetryHandler(() => false);
                fixture.caught.SetActive(true);
                fixture.input.SetGameplayBlocked(true);
                Time.timeScale = 0f;
                fixture.retry.onClick.Invoke();
                Assert.That(fixture.caught.activeSelf, Is.True);
                Assert.That(ReadGameplayBlocked(fixture.input), Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(0f));
            }
            finally { DestroyFixture(fixture); }
        }

        [Test]
        public void RestoredRetryClosesCaughtScreenAndUnblocksInput()
        {
            var fixture = CreateHudFixture(true);
            try
            {
                fixture.hud.SetRetryHandler(() =>
                {
                    InvokePrivate(fixture.hud, "OnCheckpointRestored", "cell-start");
                    return true;
                });
                fixture.caught.SetActive(true);
                fixture.input.SetGameplayBlocked(true);
                Time.timeScale = 0f;
                fixture.retry.onClick.Invoke();
                Assert.That(fixture.caught.activeSelf, Is.False);
                Assert.That(ReadGameplayBlocked(fixture.input), Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally { DestroyFixture(fixture); }
        }

        private static Fixture CreateHudFixture(bool includeRetry = false)
        {
            Time.timeScale = 1f;
            var inputObject = new GameObject("Input");
            inputObject.SetActive(false);
            var input = inputObject.AddComponent<PlayerInputReader>();
            var hudObject = new GameObject("HUD");
            hudObject.SetActive(false);
            var pause = new GameObject("Pause");
            pause.transform.SetParent(hudObject.transform, false);
            var caught = new GameObject("Caught");
            caught.transform.SetParent(hudObject.transform, false);
            var retryObject = new GameObject("Retry", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            retryObject.transform.SetParent(caught.transform, false);
            var retry = retryObject.GetComponent<Button>();
            var hud = hudObject.AddComponent<GameHudController>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("input").objectReferenceValue = input;
            serialized.FindProperty("pausePanel").objectReferenceValue = pause;
            serialized.FindProperty("capturedPanel").objectReferenceValue = caught;
            if (includeRetry) serialized.FindProperty("retryButton").objectReferenceValue = retry;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pause.SetActive(false);
            caught.SetActive(false);
            hudObject.SetActive(true);
            // EditMode tests do not guarantee MonoBehaviour lifecycle callbacks. Rebind
            // deterministically so Button.onClick exercises the same listener path as play.
            InvokePrivate(hud, "OnDisable");
            InvokePrivate(hud, "OnEnable");
            return new Fixture { hudObject = hudObject, inputObject = inputObject, input = input, hud = hud, pause = pause, caught = caught, retry = retry };
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Expected lifecycle/callback method " + methodName);
            method.Invoke(target, arguments);
        }

        private static bool ReadGameplayBlocked(PlayerInputReader input)
        {
            var field = typeof(PlayerInputReader).GetField("_gameplayBlocked", BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)field.GetValue(input);
        }

        private static AudioSource ReadAudioSource(AudioService service, string fieldName)
        {
            var field = typeof(AudioService).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (AudioSource)field.GetValue(service);
        }

        private static void DestroyFixture(Fixture fixture)
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(fixture.hudObject);
            Object.DestroyImmediate(fixture.inputObject);
        }

        private sealed class Fixture
        {
            public GameObject hudObject;
            public GameObject inputObject;
            public PlayerInputReader input;
            public GameHudController hud;
            public GameObject pause;
            public GameObject caught;
            public Button retry;
        }
    }
}
