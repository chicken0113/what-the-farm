using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FarmPlot : MonoBehaviour
    {
        [SerializeField] private Material dryMaterial;
        [SerializeField] private Material wetMaterial;
        [SerializeField] private Renderer visual;
        private readonly System.Collections.Generic.List<FleeingCrop> crops = new();
        private FarmItem weed;
        private SoilSurface surface;
        [SerializeField, Range(0, 100)] private float waterAmount;
        private bool growthStarted;
        public float WaterAmount => Mathf.Clamp(waterAmount, 0, 100);
        public bool GrowthStarted => growthStarted;
        public SoilType SoilType => surface != null ? surface.Type : null;
        public SoilSurface Surface => surface;
        public Vector3 PlantPosition => weed != null ? weed.transform.position : crops.Count > 0 && crops[0] != null ? crops[0].transform.position : transform.position;
        public float GetLight(Vector3 point, Transform plant = null) => surface != null ? surface.GetLight(point, plant) : 80;
        public void BindSurface(SoilSurface soil)
        {
            surface = soil;
            waterAmount = soil.InitialWaterAmount;
            growthStarted = waterAmount > 0;
            RefreshWaterVisual();
        }
        public bool IsTilled { get; private set; }
        public bool IsWatered => WaterAmount > 0;
        public bool IsOccupied => crops.Count > 0 || weed != null;
        public bool Contains(FleeingCrop crop) => crops.Contains(crop);
        public bool ContainsPoint(Vector3 point) =>
            new Vector2(point.x - transform.position.x, point.z - transform.position.z).sqrMagnitude
                <= Radius * Radius && Mathf.Abs(point.y - transform.position.y) < .05f;
        public float Radius { get; private set; }
        public bool FitsItem(Bounds offsets, Vector3 position)
        {
            // Check the complete horizontal footprint at the mouse position, including a small soil margin.
            float available = Mathf.Max(0, Radius - .005f);
            for (int corner = 0; corner < 4; corner++)
            {
                float x = position.x + ((corner & 1) == 0 ? offsets.min.x : offsets.max.x) - transform.position.x;
                float z = position.z + ((corner & 2) == 0 ? offsets.min.z : offsets.max.z) - transform.position.z;
                if (x * x + z * z > available * available) return false;
            }
            return true;
        }

        public void ConfigureArea(Renderer areaVisual, Material dry, Material wet, float radius)
        {
            visual = areaVisual;
            dryMaterial = dry;
            wetMaterial = wet;
            Radius = radius;
            IsTilled = true;
            visual.sharedMaterial = dryMaterial;
        }

        private void OnDestroy()
        {
            if (Radius > 0 && TryGetComponent(out MeshFilter filter))
            {
                if (Application.isPlaying) Destroy(filter.sharedMesh);
                else DestroyImmediate(filter.sharedMesh);
            }
        }

        public bool Plant(FleeingCrop plantedCrop)
        {
            if (!IsTilled || IsOccupied || plantedCrop == null)
                return false;

            crops.Add(plantedCrop);
            return true;
        }
        public bool PlantWeed(FarmItem item)
        {
            if (!IsTilled || IsOccupied || item == null || item.Kind != ItemKind.Weed) return false;
            weed = item;
            item.MarkWeedPlanted(this);
            return true;
        }
        public void ClearWeed(FarmItem item)
        {
            if (weed != item) return;
            weed = null;
            ResetEmptySoil();
        }
        private void ResetEmptySoil()
        {
            if (IsOccupied) return;
            waterAmount = surface != null ? surface.InitialWaterAmount : 0;
            growthStarted = waterAmount > 0;
            RefreshWaterVisual();
        }

        public void Water(float amount = 100)
        {
            if (!IsTilled || !IsOccupied)
                return;

            waterAmount = Mathf.Clamp(waterAmount + Mathf.Max(0, amount), 0, 100);
            if (amount > 0) growthStarted = true;
            RefreshWaterVisual();
        }

        private void RefreshWaterVisual()
        {
            if (visual != null) visual.sharedMaterial = IsWatered ? wetMaterial : dryMaterial;
        }

        public void Clear(FleeingCrop harvestedCrop)
        {
            if (!crops.Remove(harvestedCrop))
                return;

            ResetEmptySoil();
        }
    }
}
