using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FarmPlot : MonoBehaviour
    {
        [SerializeField] private Material dryMaterial;
        [SerializeField] private Material wetMaterial;
        [SerializeField] private Renderer visual;
        private FleeingCrop crop;
        public bool IsTilled { get; private set; }
        public bool IsWatered { get; private set; }
        public bool IsOccupied => crop != null;
        public FleeingCrop Crop => crop;
        public float Radius { get; private set; }

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

            crop = plantedCrop;
            IsWatered = false;
            visual.sharedMaterial = dryMaterial;
            return true;
        }

        public void Water()
        {
            if (!IsTilled || !IsOccupied)
                return;

            IsWatered = true;
            visual.sharedMaterial = wetMaterial;
        }

        public void Clear(FleeingCrop harvestedCrop)
        {
            if (crop != harvestedCrop)
                return;

            crop = null;
            IsWatered = false;
            visual.sharedMaterial = dryMaterial;
        }
    }
}
