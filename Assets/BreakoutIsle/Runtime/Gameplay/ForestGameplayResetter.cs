using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreakoutIsle.Gameplay
{
    /// <summary>Checkpoint/scene reset hub. Call from PlayerMotor.RestoreAt after physics position is restored.</summary>
    public static class ForestGameplayResetter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InstallSceneReset()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ScentTrailSystem.Reset();
        }

        public static void ResetTransientState()
        {
            ScentTrailSystem.Reset();
            MeatDistractionSource.ResetAllTransientSources();
            PoliceDogBrain.ResetAllTransientState();
            foreach (var modifiers in Object.FindObjectsByType<PlayerMovementModifiers>(FindObjectsSortMode.None))
                if (modifiers != null) modifiers.ResetTransientState();
        }

        private static void OnSceneLoaded(Scene _, LoadSceneMode __) => ResetTransientState();
    }
}
