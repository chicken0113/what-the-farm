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
        [SerializeField] private ItemKind kind = ItemKind.Curio;
        [SerializeField, Min(0)] private int generation;
        [SerializeField, Min(0)] private int baseValue = 10;
        [SerializeField] private PlantGrowthProfile growthProfile;
        public ItemKind Kind => kind;
        public int Generation => generation;
        public int BaseValue => baseValue;
        public PlantGrowthProfile GrowthProfile => growthProfile;
        public void SetGrowthProfile(PlantGrowthProfile profile) => growthProfile = profile;
        public int Value => Mathf.RoundToInt(BaseValue * Mathf.Pow(1.8f, Generation));
        public bool WasThrown { get; private set; }
        public bool IsSold { get; private set; }

        private System.Action refillStock;
        public void SetStockRefill(System.Action refill) => refillStock = refill;

        public void MarkHeld()
        {
            WasThrown = false;
            System.Action refill = refillStock;
            refillStock = null;
            refill?.Invoke();
        }
        public void MarkThrown() => WasThrown = true;

        public bool ClaimSale()
        {
            if (!WasThrown || IsSold || GetComponent<FleeingCrop>() != null) return false;
            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null || body.isKinematic) return false;
            IsSold = true;
            WasThrown = false;
            return true;
        }

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
            this.kind = kind;
            this.generation = generation;
            this.baseValue = baseValue;
            gameObject.name = DisplayName;
        }
    }
}
