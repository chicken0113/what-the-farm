using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FarmPlot : MonoBehaviour
    {
        [SerializeField] private Material untilledMaterial;
        [SerializeField] private Material dryMaterial;
        [SerializeField] private Material wetMaterial;
        [SerializeField] private Renderer visual;
        private FleeingCrop crop;
        [SerializeField] private GameObject[] furrows;

        private void Awake()
        {
            if (visual != null && untilledMaterial != null)
                visual.sharedMaterial = untilledMaterial;
        }

        public bool IsTilled { get; private set; }
        public bool IsWatered { get; private set; }
        public bool IsOccupied => crop != null;
        public FleeingCrop Crop => crop;

        public void Configure(Material untilled, Material dry, Material wet, Material furrow)
        {
            untilledMaterial = untilled;
            dryMaterial = dry;
            wetMaterial = wet;
            visual = GetComponent<Renderer>();
            visual.sharedMaterial = untilledMaterial;

            furrows = new GameObject[3];
            for (int index = 0; index < furrows.Length; index++)
            {
                GameObject row = GameObject.CreatePrimitive(PrimitiveType.Cube);
                row.name = "Tilled Furrow";
                row.transform.position = transform.position + new Vector3((index - 1) * 0.43f, 0.11f, 0f);
                row.transform.localScale = new Vector3(0.13f, 0.055f, 1.38f);
                row.transform.SetParent(transform, true);
                row.GetComponent<Renderer>().sharedMaterial = furrow;
                row.GetComponent<Collider>().enabled = false;
                row.SetActive(false);
                furrows[index] = row;
            }
        }

        public bool Till()
        {
            if (IsTilled)
                return false;

            IsTilled = true;
            visual.sharedMaterial = dryMaterial;
            foreach (GameObject row in furrows)
                row.SetActive(true);
            return true;
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
