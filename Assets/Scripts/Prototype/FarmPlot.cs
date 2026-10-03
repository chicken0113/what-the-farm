using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FarmPlot : MonoBehaviour
    {
        [SerializeField] private Material dryMaterial;
        [SerializeField] private Material wetMaterial;
        [SerializeField] private Renderer visual;
        private readonly System.Collections.Generic.List<FleeingCrop> crops = new();
        public bool IsTilled { get; private set; }
        public bool IsWatered { get; private set; }
        public bool IsOccupied => crops.Count > 0;
        public bool Contains(FleeingCrop crop) => crops.Contains(crop);
        public bool ContainsPoint(Vector3 point) =>
            new Vector2(point.x - transform.position.x, point.z - transform.position.z).sqrMagnitude
                <= Radius * Radius && Mathf.Abs(point.y - transform.position.y) < .05f;
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
            if (!IsTilled || plantedCrop == null || crops.Contains(plantedCrop))
                return false;

            crops.Add(plantedCrop);
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
            if (!crops.Remove(harvestedCrop))
                return;

            if (!IsOccupied)
            {
                IsWatered = false;
                visual.sharedMaterial = dryMaterial;
            }
        }
    }
}
