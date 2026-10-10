using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public enum ItemKind
    {
        Seed,
        Produce,
        Tool,
        WateringCan,
        Curio,
        Corpse,
        Weed
    }

    public sealed class FarmItem : MonoBehaviour
    {
        [SerializeField] private ItemKind kind = ItemKind.Curio;
        [SerializeField, Min(0)] private int generation;
        [SerializeField, Min(0)] private int baseValue = 10;
        [SerializeField, Tooltip("Optional unique price ID for a new item. Empty uses the built-in kind/state price.")]
        private string customPriceId;
        [SerializeField] private bool sizePriced;
        [SerializeField] private string plantDisplayName;
        public bool SizePriced => sizePriced;
        public void SetPlantIdentity(string name) { plantDisplayName = name; sizePriced = true; }
        public string CustomPriceId => customPriceId;
        public void SetPriceId(string id) => customPriceId = id;
        public string PriceId => string.IsNullOrWhiteSpace(customPriceId) ? ItemPriceCatalog.DefaultId(this) : customPriceId;
        public int PurchasePrice => ItemPriceCatalog.Purchase(this);
        [SerializeField] private PlantGrowthProfile growthProfile;
        [SerializeField] private Vector3 originalScale;
        [SerializeField] private bool hasBeenPlanted;
        private void Awake() => SetLooseCollision();
        private FarmPlot weedPlot;
        public bool IsPlantedWeed => weedPlot != null;
        public void MarkWeedPlanted(FarmPlot plot) { weedPlot = plot; MarkPlanted(); }
        public bool HasBeenPlanted => hasBeenPlanted;
        public bool CanUseTool => GetComponent<GrowableTool>() == null || hasBeenPlanted;
        // Measure the visible model in its planted orientation, not its pose in the player's hand.
        // Bounds are offsets from the planting point, so off-centre model pivots are preserved.
        public Bounds PlantingBounds(Quaternion rotation)
        {
            Bounds result = new Bounds(Vector3.zero, Vector3.zero);
            bool found = false;
            Vector3 scale = transform.lossyScale;
            void Include(Vector3 local, Transform part)
            {
                Vector3 offset = rotation * Vector3.Scale(transform.InverseTransformPoint(part.TransformPoint(local)), scale);
                if (!found) { result = new Bounds(offset, Vector3.zero); found = true; }
                else result.Encapsulate(offset);
            }
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || renderer is LineRenderer || renderer is TrailRenderer || renderer is ParticleSystemRenderer) continue;
                bool visible = true;
                for (var part = renderer.transform; part != transform; part = part.parent)
                    if (!part.gameObject.activeSelf) { visible = false; break; }
                if (!visible) continue;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    var mesh = new Mesh(); skin.BakeMesh(mesh, false);
                    foreach (var vertex in mesh.vertices) Include(vertex, skin.transform);
                    if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
                }
                else
                {
                    Bounds bounds = renderer.localBounds;
                    for (int corner = 0; corner < 8; corner++)
                        Include(bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                            (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)), renderer.transform);
                }
            }
            return result;
        }
        public void MarkPlanted() { hasBeenPlanted = true; GetComponent<GrowableTool>()?.ShowComplete(); }
        public void SetLooseCollision()
        {
            foreach (Transform part in GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 8;
            Physics.IgnoreLayerCollision(8, 9, true);
            Physics.IgnoreLayerCollision(8, 8, true);
        }
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
        public int Value => ItemPriceCatalog.Sale(this);
        public bool WasThrown { get; private set; }
        public bool IsSold { get; private set; }

        private System.Action refillStock;
        public bool IsShopStock => refillStock != null;
        private readonly System.Collections.Generic.List<Material> ownedMaterials = new();
        public void OwnMaterials(System.Collections.Generic.IEnumerable<Material> materials) => ownedMaterials.AddRange(materials);
        private void OnDestroy()
        {
            weedPlot?.ClearWeed(this);
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
            weedPlot?.ClearWeed(this);
            weedPlot = null;
            WasThrown = false;
            System.Action refill = refillStock;
            refillStock = null;
            refill?.Invoke();
        }
        public void MarkThrown() => WasThrown = true;

        public bool ClaimSale()
        {
            if (!WasThrown || IsSold || IsPlantedWeed || GetComponent<FleeingCrop>() != null) return false;
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
                if (!string.IsNullOrWhiteSpace(plantDisplayName)) return plantDisplayName;
                if (Kind == ItemKind.Corpse) return GetComponent<PlantableCorpse>()?.DisplayName ?? "Body";
                string name = Kind switch
                {
                    ItemKind.Seed => "Seed",
                    ItemKind.Produce => "Crop",
                    ItemKind.Tool => GetComponent<GrowableTool>() != null ? (CanUseTool ? "Shovel" : "Shovel Head") : "Hoe",
                    ItemKind.WateringCan => "Watering Can",
                    ItemKind.Weed => "Weed",
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
            SetLooseCollision();
            gameObject.name = DisplayName;
        }
    }
}
