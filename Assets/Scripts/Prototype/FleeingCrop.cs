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
        private Renderer visual;

        public bool IsMature => growthProgress >= growthTime;
        public bool IsWatered => plot != null && plot.IsWatered;
        public FarmPlot Plot => plot;
        public int Generation => generation;
        public int Value => Mathf.RoundToInt(baseValue * Mathf.Pow(1.8f, generation));
        public float Health => health;
        public float MaxHealth => maxHealth;

        public void Configure(FarmPrototype prototype, FarmItem source, FarmPlot homePlot)
        {
            world = prototype;
            plot = homePlot;
            sourceKind = source.Kind;
            generation = source.Generation + (source.Kind == ItemKind.Seed ? 0 : 1);
            baseValue = source.BaseValue;
            growthTime = Mathf.Max(1.5f, 4f + generation);
            maxHealth = 3f + generation * 2f;
            health = maxHealth;
            visual = GetComponent<Renderer>();
            transform.localScale = Vector3.one * 0.15f;
            gameObject.name = $"Growing {source.DisplayName}";
        }

        private void Update()
        {
            if (!IsMature)
            {
                if (!IsWatered)
                    return;

                growthProgress = Mathf.Min(growthTime, growthProgress + Time.deltaTime);
                float size = Mathf.Lerp(0.15f, 1f + generation * 0.16f, growthProgress / growthTime);
                transform.localScale = Vector3.one * size;
                Vector3 position = transform.position;
                position.y = plot.transform.position.y + size;
                transform.position = position;
                if (IsMature)
                {
                    gameObject.name = $"Mature crop +{generation}";
                    visual.material.color = new Color(1f, 0.48f, 0.13f);
                }
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

        public void TakeHit(float damage)
        {
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
            world.CreateItem(resultKind, generation, baseValue, transform.position + Vector3.up * 0.5f);
            world.SetMessage($"Harvested! Pick up and replant for a more valuable, tougher crop.");
            plot.Clear(this);
            Destroy(gameObject);
        }
    }
}
