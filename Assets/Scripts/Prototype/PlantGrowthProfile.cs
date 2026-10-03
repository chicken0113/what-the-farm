using UnityEngine;

namespace WhatTheFarm.Prototype
{
    [CreateAssetMenu(menuName = "What The Farm/Plant Growth Profile")]
    public sealed class PlantGrowthProfile : ScriptableObject
    {
        [Header("Base growth")]
        [Min(0)] public float baseRatePercent = 100;
        public bool requireWaterToStart = true;
        [Min(.1f)] public float growthSeconds = 4;
        [Min(0)] public float secondsPerGeneration = 1;
        [Min(1)] public float matureSizeMultiplier = 2;
        [Min(0)] public float sizePerGeneration = .32f;

        [Header("Preferred light (0-100)")]
        public bool useLightCondition = true;
        [Range(0, 100)] public float minLight = 60;
        [Range(0, 100)] public float maxLight = 100;
        [Min(0)] public float lightBonusPercent = 25;

        [Header("Preferred water amount (0-100)")]
        public bool useWaterCondition = true;
        [Range(0, 100)] public float minWater = 40;
        [Range(0, 100)] public float maxWater = 80;
        [Min(0)] public float waterBonusPercent = 25;

        [Header("Preferred soil types")]
        public bool useSoilCondition = true;
        public SoilType[] preferredSoils = System.Array.Empty<SoilType>();
        [Min(0)] public float soilBonusPercent = 20;

        public float Evaluate(float light, float water, SoilType soil)
        {
            float rate = Mathf.Max(0, baseRatePercent);
            if (useLightCondition && Matches(light, minLight, maxLight)) rate += Mathf.Max(0, lightBonusPercent);
            if (useWaterCondition && Matches(water, minWater, maxWater)) rate += Mathf.Max(0, waterBonusPercent);
            if (useSoilCondition && soil != null && preferredSoils != null &&
                System.Array.IndexOf(preferredSoils, soil) >= 0) rate += Mathf.Max(0, soilBonusPercent);
            return rate;
        }

        private static bool Matches(float value, float min, float max) =>
            value >= Mathf.Min(min, max) && value <= Mathf.Max(min, max);
    }
}
