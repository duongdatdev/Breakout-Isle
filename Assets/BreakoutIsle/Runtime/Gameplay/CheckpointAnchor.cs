using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public interface ICheckpointPlayerAdapter
    {
        // The adapter must also reset velocity/input/transient player state and restore full stamina.
        void RestoreAt(Vector3 position);
    }

    public sealed class CheckpointAnchor : MonoBehaviour
    {
        [SerializeField] private string checkpointId;
        [SerializeField] private Transform safeSpawn;

        public string CheckpointId => checkpointId;
        public Vector3 SpawnPosition => safeSpawn != null ? safeSpawn.position : transform.position;
    }
}
