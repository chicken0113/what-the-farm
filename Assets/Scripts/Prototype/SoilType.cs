using UnityEngine;

namespace WhatTheFarm.Prototype
{
    [CreateAssetMenu(menuName = "What The Farm/Soil Type")]
    public sealed class SoilType : ScriptableObject
    {
        public string DisplayName => name;
    }
}
