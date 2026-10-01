using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FarmPlot : MonoBehaviour
    {
        private Material dryMaterial;
        private Material wetMaterial;
        private Renderer visual;
        private FleeingCrop crop;

        public bool IsWatered { get; private set; }
        public bool IsOccupied => crop != null;
        public FleeingCrop Crop => crop;

        public void Configure(Material dry, Material wet)
        {
            dryMaterial = dry;
            wetMaterial = wet;
            visual = GetComponent<Renderer>();
            visual.sharedMaterial = dryMaterial;
        }

        public void Plant(FleeingCrop plantedCrop)
        {
            crop = plantedCrop;
            IsWatered = false;
            visual.sharedMaterial = dryMaterial;
        }

        public void Water()
        {
            if (!IsOccupied)
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
