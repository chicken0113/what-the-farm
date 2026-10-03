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
        [SerializeField] private Vector3 originalScale;
        public Vector3 OriginalScale
        {
            get
            {
                if (originalScale == Vector3.zero) originalScale = transform.lossyScale;
                return originalScale;
            }
        }
        public void SetOriginalScale(Vector3 scale) => originalScale = scale;
        public float SizeMultiplier
        {
            get
            {
                Vector3 baseline = OriginalScale;
                Vector3 current = transform.lossyScale;
                return Mathf.Max(Mathf.Abs(current.x) / Mathf.Max(.0001f, Mathf.Abs(baseline.x)),
                    Mathf.Abs(current.y) / Mathf.Max(.0001f, Mathf.Abs(baseline.y)),
                    Mathf.Abs(current.z) / Mathf.Max(.0001f, Mathf.Abs(baseline.z)));
            }
        }
        public ItemKind Kind => kind;
        public int Generation => generation;
        public int BaseValue => baseValue;
        public PlantGrowthProfile GrowthProfile => growthProfile;
        public void SetGrowthProfile(PlantGrowthProfile profile) => growthProfile = profile;
        public int Value => Mathf.RoundToInt(BaseValue * Mathf.Pow(1.8f, Generation));
        public bool WasThrown { get; private set; }
        public bool IsSold { get; private set; }

        private System.Action refillStock;
        private readonly System.Collections.Generic.List<Material> ownedMaterials = new();
        public void OwnMaterials(System.Collections.Generic.IEnumerable<Material> materials) => ownedMaterials.AddRange(materials);
        private void OnDestroy()
        {
            foreach (var material in ownedMaterials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }
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
            if (originalScale == Vector3.zero) originalScale = transform.lossyScale;
            this.kind = kind;
            this.generation = generation;
            this.baseValue = baseValue;
            gameObject.name = DisplayName;
        }
    }
}
