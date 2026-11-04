#if BREAKOUT_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BreakoutIsle.Gameplay;
using BreakoutIsle.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BreakoutIsle.StandaloneQA
{
    public sealed class BreakoutStandaloneQaProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Report
        {
            public string unityVersion;
            public string isolatedProfilePath;
            public string screenshotsDirectory;
            public bool newGameStarted;
            public bool pauseOpened;
            public bool pauseResumed;
            public bool tabMapOpened;
            public bool returnedToMenu;
            public bool continueStarted;
            public bool movementPassed = true;
            public List<MovementMeasurement> movement = new List<MovementMeasurement>();
            public List<string> captures = new List<string>();
            public List<string> notes = new List<string>();
        }

        [Serializable]
        private sealed class MovementMeasurement
        {
            public string mode;
            public Vector2 start;
            public Vector2 end;
            public Vector2 delta;
            public float durationSeconds;
            public bool resetToSafeSpawn;
            public bool moved;
        }

        private const string BootstrapScene = "Bootstrap";
        private const string StoreFieldName = "_store";
        private static BreakoutStandaloneQaProbe _instance;
        private Keyboard _keyboard;
        private string _runDirectory;
        private string _profilePath;
        private Report _report;
        private bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeFirstScene()
        {
            if (_instance != null) return;
            var host = new GameObject("Breakout Standalone QA Probe");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<BreakoutStandaloneQaProbe>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            PrepareOutputPaths();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == BootstrapScene && FindFirstObjectByType<MainMenuController>() != null &&
                FindFirstObjectByType<CheckpointSaveCoordinator>() == null)
            {
                // Bootstrap has no gameplay coordinator in the shipping scene. This QA-only instance
                // lets Continue read the isolated profile before MainMenuController.Start runs.
                new GameObject("QA Isolated Menu Save Coordinator").AddComponent<CheckpointSaveCoordinator>();
            }
            InjectIsolatedStoreIntoEveryCoordinator();
        }

        private IEnumerator Start()
        {
            if (_started) yield break;
            _started = true;
            _keyboard = InputSystem.AddDevice<Keyboard>("Breakout QA virtual keyboard");
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return WaitForResolution(1280, 720);

            if (SceneManager.GetActiveScene().name != BootstrapScene)
            {
                SceneManager.LoadScene(BootstrapScene);
                yield return WaitForMenu();
            }
            else yield return WaitForMenu();

            yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("menu-1280x720.png");
            var menu = FindFirstObjectByType<MainMenuController>();
            var newGame = ReadPrivate<Button>(menu, "newGameButton");
            if (newGame == null)
            {
                _report.notes.Add("Main menu New Game button was not found.");
                Finish();
                yield break;
            }

            yield return Submit(newGame);
            yield return WaitForGameplay();
            _report.newGameStarted = FindFirstObjectByType<LevelSession>() != null && FindFirstObjectByType<PlayerMotor>() != null;
            if (!_report.newGameStarted)
            {
                _report.notes.Add("New Game did not reach a level with a player and session before the timeout.");
                Finish();
                yield break;
            }

            yield return NeutralInput();
            yield return Capture("gameplay-new-1280x720.png");
            yield return MeasureMove("walk", new[] { Key.D }, 0.35f);
            yield return MeasureMove("sprint", new[] { Key.D, Key.LeftShift }, 0.35f);
            yield return MeasureMove("sneak", new[] { Key.D, Key.LeftCtrl }, 0.35f);

            yield return Tap(Key.Tab);
            yield return new WaitForSecondsRealtime(0.2f);
            var hud = FindFirstObjectByType<GameHudController>();
            var mapPanel = ReadPrivate<GameObject>(hud, "objectivesPanel");
            _report.tabMapOpened = mapPanel != null && mapPanel.activeInHierarchy;
            yield return Capture("map-1280x720.png");
            yield return Tap(Key.Tab);

            yield return Tap(Key.Escape);
            yield return new WaitForSecondsRealtime(0.2f);
            var pausePanel = ReadPrivate<GameObject>(hud, "pausePanel");
            _report.pauseOpened = pausePanel != null && pausePanel.activeInHierarchy;
            yield return Capture("pause-1280x720.png");
            yield return Tap(Key.Escape);
            yield return new WaitForSecondsRealtime(0.2f);
            _report.pauseResumed = pausePanel == null || !pausePanel.activeInHierarchy;

            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return WaitForResolution(1920, 1080);
            yield return Capture("gameplay-1920x1080.png");
            yield return MeasureMove("walk-1920x1080", new[] { Key.D }, 0.35f);

            hud = FindFirstObjectByType<GameHudController>();
            var menuButton = ReadPrivate<Button>(hud, "returnToMenuButton");
            if (menuButton != null)
            {
                yield return Tap(Key.Escape);
                yield return new WaitForSecondsRealtime(0.2f);
                yield return Submit(menuButton);
                yield return WaitForMenu();
                _report.returnedToMenu = FindFirstObjectByType<MainMenuController>() != null;
            }
            else _report.notes.Add("HUD return-to-menu button was not found.");

            if (_report.returnedToMenu)
            {
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture("menu-1920x1080.png");
                menu = FindFirstObjectByType<MainMenuController>();
                var continueButton = ReadPrivate<Button>(menu, "continueButton");
                if (continueButton != null && continueButton.interactable)
                {
                    yield return Submit(continueButton);
                    yield return WaitForGameplay();
                    _report.continueStarted = FindFirstObjectByType<LevelSession>() != null && FindFirstObjectByType<PlayerMotor>() != null;
                    if (_report.continueStarted)
                        yield return Capture("gameplay-continue-1920x1080.png");
                    else _report.notes.Add("Continue did not reach a level with a player and session before the timeout.");
                }
                else _report.notes.Add("Continue was not interactable after returning to Bootstrap.");
            }

            Finish();
        }

        private void PrepareOutputPaths()
        {
            var requested = GetCommandLineValue("-breakoutQaOutput");
            var root = string.IsNullOrWhiteSpace(requested)
                ? Path.Combine(Path.GetTempPath(), "BreakoutIsleStandaloneQA")
                : Path.GetFullPath(requested);
            _runDirectory = Path.Combine(root, "run-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            var captures = Path.Combine(_runDirectory, "screenshots");
            Directory.CreateDirectory(captures);
            _profilePath = Path.Combine(_runDirectory, "isolated-profile.json");
            _report = new Report
            {
                unityVersion = Application.unityVersion,
                isolatedProfilePath = _profilePath,
                screenshotsDirectory = captures
            };
        }

        private void InjectIsolatedStoreIntoEveryCoordinator()
        {
            var storeField = typeof(CheckpointSaveCoordinator).GetField(StoreFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (storeField == null)
            {
                _report.notes.Add("CheckpointSaveCoordinator private store field was not found; profile isolation could not be guaranteed.");
                return;
            }

            foreach (var coordinator in FindObjectsByType<CheckpointSaveCoordinator>(FindObjectsSortMode.None))
                storeField.SetValue(coordinator, new AtomicJsonSaveStore(_profilePath));
        }

        private IEnumerator MeasureMove(string mode, Key[] keys, float seconds)
        {
            SetKeys();
            yield return new WaitForSecondsRealtime(0.2f);
            var motor = FindFirstObjectByType<PlayerMotor>();
            if (motor == null)
            {
                _report.notes.Add($"Movement sample '{mode}' skipped because PlayerMotor was missing.");
                _report.movementPassed = false;
                yield break;
            }

            var safeSpawn = new Vector3(4.5f, 4.5f, 0f);
            var body = motor.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = safeSpawn;
            }
            motor.transform.position = safeSpawn;
            Physics2D.SyncTransforms();
            yield return null;

            var sample = new MovementMeasurement
            {
                mode = mode,
                start = motor.transform.position,
                durationSeconds = seconds,
                resetToSafeSpawn = true
            };
            SetKeys(keys);
            yield return new WaitForSecondsRealtime(seconds);
            SetKeys();
            yield return new WaitForSecondsRealtime(0.2f);
            sample.end = motor.transform.position;
            sample.delta = sample.end - sample.start;
            sample.moved = sample.delta.sqrMagnitude > 0.01f;
            if (!sample.moved)
            {
                _report.movementPassed = false;
                _report.notes.Add($"Movement sample '{mode}' was blocked or below 0.1m (measured {sample.delta.magnitude:F3}m).");
            }
            _report.movement.Add(sample);
        }

        private IEnumerator Submit(Button button)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
            yield return null;
            yield return Tap(Key.Enter);
        }

        private IEnumerator Tap(Key key)
        {
            SetKeys(key);
            yield return new WaitForSecondsRealtime(0.08f);
            SetKeys();
            yield return new WaitForSecondsRealtime(0.12f);
        }

        private IEnumerator NeutralInput()
        {
            SetKeys();
            yield return new WaitForSecondsRealtime(0.2f);
        }

        private void SetKeys(params Key[] keys)
        {
            if (_keyboard != null) InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
        }

        private IEnumerator WaitForGameplay()
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (FindFirstObjectByType<LevelSession>() != null && FindFirstObjectByType<PlayerMotor>() != null)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield break;
                }
                yield return null;
            }
        }

        private IEnumerator WaitForMenu()
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline && FindFirstObjectByType<MainMenuController>() == null)
                yield return null;
        }

        private IEnumerator WaitForResolution(int width, int height)
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && (Screen.width != width || Screen.height != height))
                yield return null;
            if (Screen.width != width || Screen.height != height)
                _report.notes.Add($"Requested {width}x{height}; active screen is {Screen.width}x{Screen.height}.");
            yield return new WaitForSecondsRealtime(0.25f);
        }

        private IEnumerator Capture(string filename)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(_report.screenshotsDirectory, filename);
            ScreenCapture.CaptureScreenshot(path);
            _report.captures.Add(path);
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && (!File.Exists(path) || new FileInfo(path).Length == 0))
                yield return null;
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                _report.notes.Add("Screenshot was not written: " + filename);
        }

        private void Finish()
        {
            var reportPath = Path.Combine(_runDirectory, "qa-report.json");
            File.WriteAllText(reportPath, JsonUtility.ToJson(_report, true));
            Debug.Log($"Standalone QA probe complete. Report: {reportPath}");
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            Application.Quit();
        }

        private static T ReadPrivate<T>(object instance, string fieldName) where T : class
        {
            if (instance == null) return null;
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(instance) as T;
        }

        private static string GetCommandLineValue(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(key + "=", StringComparison.Ordinal)) return args[i].Substring(key.Length + 1).Trim('"');
                if (args[i] == key && i + 1 < args.Length) return args[i + 1].Trim('"');
            }
            return null;
        }
    }
}
#endif
