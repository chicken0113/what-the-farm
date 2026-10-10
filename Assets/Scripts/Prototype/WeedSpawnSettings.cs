using UnityEngine;

namespace WhatTheFarm.Prototype
{
    [CreateAssetMenu(menuName = "What The Farm/Weed Spawning")]
    public sealed class WeedSpawnSettings : ScriptableObject
    {
        public bool enabled = true;
        public FarmItem prefab;
        [Min(.25f)] public float intervalSeconds = 20;
        [Min(0)] public int initialCount = 12;
        [Min(1)] public int countPerInterval = 3;
        [Min(1)] public int maxWildWeeds = 60;
        [Min(.1f)] public float minimumSpacing = .6f;
        [Min(1)] public int placementAttempts = 32;
    }
}
