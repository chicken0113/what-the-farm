using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public enum ItemKind
    {
        Seed,
        Produce,
        Tool,
        WateringCan,
        Curio
    }

    public sealed class FarmItem : MonoBehaviour
    {
        public ItemKind Kind { get; private set; }
        public int Generation { get; private set; }
        public int BaseValue { get; private set; }
        public int Value => Mathf.RoundToInt(BaseValue * Mathf.Pow(1.8f, Generation));

        public string DisplayName
        {
            get
            {
                string name = Kind switch
                {
                    ItemKind.Seed => "Seed",
                    ItemKind.Produce => "Crop",
                    ItemKind.Tool => "Hoe",
                    ItemKind.WateringCan => "Watering Can",
                    _ => "Stone"
                };
                return Generation > 0 ? $"{name} +{Generation}" : name;
            }
        }

        public void Configure(ItemKind kind, int generation, int baseValue)
        {
            Kind = kind;
            Generation = generation;
            BaseValue = baseValue;
            gameObject.name = DisplayName;
        }
    }
}
