using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    [CreateAssetMenu(menuName = "Breakout Isle/Campaign Level", fileName = "CampaignLevel")]
    public sealed class CampaignLevelDefinition : ScriptableObject
    {
        public string levelId;
        public string sceneName;
        public string initialCheckpointId;
        public CampaignLevelDefinition nextLevel;
        public bool isFinalLevel;
        public string[] carryQuestItemIdsToNextLevel = System.Array.Empty<string>();
    }
}
