using System;
using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public enum SupportNoisePolicy
    {
        None,
        NearestGuard,
        AllGuards
    }

    [Serializable]
    public sealed class SupportItemProfile
    {
        public string itemId;
        public Sprite projectileSprite;
        public SupportNoisePolicy noisePolicy;
        [Min(0f)] public float noiseRadius;
        [Min(0f)] public float fuseSeconds;
        public bool attractsDogs;
    }
}
