using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FleeingCrop : MonoBehaviour
    {
        private FarmPrototype world;
        private FarmPlot plot;
        private ItemKind sourceKind;
        private int generation;
        private int baseValue;
        private float growthTime;
        private float growthProgress;
        private float health;
        private float maxHealth;
        private float nextFleeDirectionTime;
        private Vector3 fleeDirection;
        private Renderer[] visuals;
        private readonly System.Collections.Generic.List<Material> ownedMaterials = new();
        private Collider[] bodies;
        private Vector3 initialScale;
        private Vector3 originalItemScale;
        private float baseOffset;
        private bool removed;
        private float groundHeight;
        [SerializeField] private PlantGrowthProfile growthProfile;
        [SerializeField] private float growthRatePercent = 100;
        public PlantGrowthProfile GrowthProfile => growthProfile;
        public float GrowthRatePercent => growthRatePercent;
        public bool IsPlanted => !removed && plot != null && plot.Contains(this);

        public bool IsMature => growthProgress >= growthTime;
        public bool IsWatered => plot != null && plot.IsWatered;
        public FarmPlot Plot => plot;
        public int Generation => generation;
        public int Value => Mathf.RoundToInt(baseValue * Mathf.Pow(1.8f, generation));
        public float Health => health;
        public float MaxHealth => maxHealth;

        public void Configure(FarmPrototype prototype, FarmItem source, FarmPlot homePlot, float soilHeight)
        {
            world = prototype;
            plot = homePlot;
            groundHeight = soilHeight;
            initialScale = transform.localScale;
            originalItemScale = source.OriginalScale;
            bodies = GetComponentsInChildren<Collider>();
            sourceKind = source.Kind;
            generation = source.Generation;
            baseValue = source.BaseValue;
            growthProfile = source.GrowthProfile;
            growthTime = growthProfile != null
                ? Mathf.Max(.1f, growthProfile.growthSeconds + generation * growthProfile.secondsPerGeneration)
                : Mathf.Max(1.5f, 4f + generation);
            maxHealth = 3f + generation * 2f;
            health = maxHealth;
            visuals = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in visuals)
                ownedMaterials.AddRange(renderer.sharedMaterials);
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in visuals)
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found)
                foreach (Collider body in bodies)
                {
                    if (!found) { bounds = body.bounds; found = true; }
                    else bounds.Encapsulate(body.bounds);
                }
            baseOffset = transform.position.y - bounds.min.y;
            Vector3 position = transform.position;
            position.y = groundHeight + baseOffset;
            transform.position = position;
            gameObject.name = $"Growing {source.DisplayName}";
        }

        private void Update()
        {
            if (removed) return;
            if (!IsMature)
            {
                Grow(Time.deltaTime);
                return;
            }

            Vector3 away = transform.position - world.PlayerPosition;
            away.y = 0f;
            if (away.sqrMagnitude > 36f || away.sqrMagnitude < 0.01f)
                return;

            if (Time.time >= nextFleeDirectionTime)
            {
                fleeDirection = (away.normalized + new Vector3(Random.Range(-0.35f, 0.35f), 0f,
                    Random.Range(-0.35f, 0.35f))).normalized;
                nextFleeDirectionTime = Time.time + 0.65f;
            }

            Vector3 next = transform.position + fleeDirection * (2f + generation * 0.35f) * Time.deltaTime;
            next.x = Mathf.Clamp(next.x, -world.ArenaHalfSize, world.ArenaHalfSize);
            next.z = Mathf.Clamp(next.z, -world.ArenaHalfSize, world.ArenaHalfSize);
            transform.position = next;
            transform.Rotate(Vector3.up, 95f * Time.deltaTime, Space.World);
        }

        public void Grow(float elapsed)
        {
            if (!IsPlanted || IsMature || elapsed <= 0) return;
            growthRatePercent = growthProfile != null
                ? growthProfile.Evaluate(plot.GetLight(transform.position, transform), plot.WaterAmount, plot.SoilType)
                : 100;
            if ((growthProfile == null || growthProfile.requireWaterToStart) && !plot.GrowthStarted) return;
            growthProgress = Mathf.Min(growthTime, growthProgress + elapsed);
            float baseSize = growthProfile != null
                ? growthProfile.matureSizeMultiplier + generation * growthProfile.sizePerGeneration
                : 2f + generation * .32f;
            float finalSize = Mathf.Max(1, baseSize * growthRatePercent / 100);
            float size = Mathf.Lerp(1f, finalSize, growthProgress / growthTime);
            transform.localScale = initialScale * size;
            Vector3 position = transform.position;
            position.y = groundHeight + baseOffset * size;
            transform.position = position;
            if (IsMature)
            {
                gameObject.name = $"Mature crop +{generation}";
                foreach (Renderer renderer in visuals)
                    foreach (Material material in renderer.sharedMaterials)
                        if (material != null) material.color = new Color(1f, .48f, .13f);
                ReleaseSoil();
            }
        }

        private void ReleaseSoil()
        {
            if (plot != null) plot.Clear(this);
            plot = null;
        }

        private void OnDestroy()
        {
            ReleaseSoil();
            foreach (Material material in ownedMaterials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }

        public void TakeHit(float damage)
        {
            if (removed) return;
            if (!IsMature)
            {
                world.SetMessage("Wait until the crop is fully grown.");
                return;
            }

            health -= damage;
            if (health > 0f)
            {
                world.SetMessage($"Crop hit! {Mathf.CeilToInt(health)}/{Mathf.CeilToInt(maxHealth)} HP");
                return;
            }

            ItemKind resultKind = sourceKind == ItemKind.Seed ? ItemKind.Produce : sourceKind;
            // Turn the grown model into a pickup so its size, mesh and child transforms survive harvest.
            removed = true;
            ReleaseSoil();
            FarmItem harvested = gameObject.AddComponent<FarmItem>();
            harvested.Configure(resultKind, generation, baseValue);
            harvested.SetOriginalScale(originalItemScale);
            harvested.SetGrowthProfile(growthProfile);
            harvested.MarkPlanted();
            harvested.OwnMaterials(ownedMaterials);
            ownedMaterials.Clear();
            transform.position += Vector3.up * .5f;
            var rigidbody = GetComponent<Rigidbody>();
            if (rigidbody == null) rigidbody = gameObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = false;
            rigidbody.useGravity = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var collider in bodies) collider.enabled = true;
            world.SetMessage("Harvested! Use or sell it. Each item can only be planted once.");
            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
        }
    }
}
