using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [CreateAssetMenu(menuName = "Breakout Isle/Level Objectives", fileName = "LevelObjectives")]
    public sealed class LevelObjectiveSet : ScriptableObject
    {
        public string levelId;
        public ObjectiveStepDefinition[] steps;
    }
}
