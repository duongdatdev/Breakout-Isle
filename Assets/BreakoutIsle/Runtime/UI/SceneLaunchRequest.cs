using BreakoutIsle.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreakoutIsle.UI
{
    public sealed class SceneLaunchRequest : MonoBehaviour
    {
        private bool _continue;
        private bool _confirmedRestart;
        private string _scene;
        private string _checkpoint;
        private bool _loading;

        public void Configure(bool continueGame, bool confirmedRestart, string scene, string checkpoint)
        {
            _continue = continueGame;
            _confirmedRestart = confirmedRestart;
            _scene = scene;
            _checkpoint = checkpoint;
            _loading = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(_scene);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_loading || scene.name != _scene) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            var coordinator = FindObjectOfType<CheckpointSaveCoordinator>();
            if (coordinator != null)
            {
                if (_continue) coordinator.LoadContinue();
                else if (_confirmedRestart) coordinator.ConfirmRestartAndStartNewGame(_checkpoint);
                else coordinator.StartNewGame(_checkpoint);
            }
            Destroy(gameObject);
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
