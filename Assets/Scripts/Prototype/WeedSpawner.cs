using System.Collections.Generic;
using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class WeedSpawner : MonoBehaviour
    {
        [SerializeField] private WeedSpawnSettings settings;
        private FarmPrototype world;
        private FarmFirstStage boundary;
        private const float BoundaryInset = .5f;
        private readonly List<FarmItem> wild = new();
        private readonly List<Collider> grounds = new();
        private float clock;
        public int WildCount { get { ForgetCollected(); return wild.Count; } }
        public void Configure(FarmPrototype farm, WeedSpawnSettings config)
        {
            world = farm; settings = config; clock = 0;
            boundary = farm.GetComponent<FarmFirstStage>();
            grounds.Clear();
            foreach (var soil in FindObjectsByType<SoilSurface>(FindObjectsSortMode.None))
                if (soil.gameObject.scene == farm.gameObject.scene && soil.TryGetComponent<Collider>(out var collider) && collider.enabled)
                    grounds.Add(collider);
            Physics.SyncTransforms();
            for (int i = 0; i < Mathf.Min(config.initialCount, config.maxWildWeeds); i++) TrySpawn();
        }
        private void ForgetCollected() => wild.RemoveAll(item => item == null || item.transform.parent != transform || item.IsPlantedWeed);
        private void Update() => Advance(Time.deltaTime);
        public void Advance(float seconds)
        {
            if (settings == null || !settings.enabled || seconds <= 0) return;
            clock += seconds;
            if (clock < Mathf.Max(.25f, settings.intervalSeconds)) return;
            clock %= Mathf.Max(.25f, settings.intervalSeconds);
            for (int i = 0; i < settings.countPerInterval && WildCount < settings.maxWildWeeds; i++) TrySpawn();
        }
        public bool TrySpawn()
        {
            if (settings == null || !settings.enabled || settings.prefab == null || world == null || WildCount >= settings.maxWildWeeds) return false;
            float totalArea = 0;
            foreach (var ground in grounds)
                if (SpawnBounds(ground, out var area)) totalArea += area.size.x * area.size.z;
            if (totalArea <= 0) return false;
            for (int attempt = 0; attempt < settings.placementAttempts; attempt++)
            {
                float choice = Random.value * totalArea;
                Collider chosen = null;
                foreach (var ground in grounds)
                {
                    if (!SpawnBounds(ground, out var area)) continue;
                    chosen = ground; choice -= area.size.x * area.size.z;
                    if (choice <= 0) break;
                }
                if (!SpawnBounds(chosen, out var bounds)) continue;
                Vector3 top = new(Random.Range(bounds.min.x, bounds.max.x), bounds.max.y + 10, Random.Range(bounds.min.z, bounds.max.z));
                if (!Physics.Raycast(top, Vector3.down, out var hit, bounds.size.y + 20, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)) continue;
                var hitGround = hit.collider.GetComponent<FarmPlot>()?.Surface?.GetComponent<Collider>() ?? hit.collider;
                if (hitGround != chosen || hit.normal.y < .85f || !CanSpawnAt(hit.point, chosen)) continue;
                var item = Instantiate(settings.prefab, hit.point + Vector3.up * .02f, Quaternion.Euler(0, Random.Range(0, 360), 0));
                item.transform.SetParent(transform, true);
                item.Configure(ItemKind.Weed, 0, 3);
                item.GetComponent<Rigidbody>().isKinematic = true;
                item.GetComponent<Rigidbody>().useGravity = false;
                wild.Add(item);
                return true;
            }
            return false;
        }
        public bool CanSpawnAt(Vector3 point, Collider ground)
        {
            if (boundary != null && !boundary.IsInsidePeacefulArea(point, BoundaryInset)) return false;
            float spacing = Mathf.Max(.1f, settings.minimumSpacing);
            foreach (var item in wild)
                if (item != null && Vector3.Distance(point, item.transform.position) < spacing) return false;
            foreach (var collider in Physics.OverlapSphere(point + Vector3.up * .2f, .2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider == ground) continue;
                if (collider.GetComponentInParent<FarmItem>() != null || collider.GetComponentInParent<FleeingCrop>() != null) return false;
                if (collider.GetComponent<SoilSurface>() != null || collider.GetComponent<FarmPlot>() != null) continue;
                return false;
            }
            return true;
        }
        private bool SpawnBounds(Collider ground, out Bounds result)
        {
            result = default;
            if (ground == null || !ground.enabled || !ground.gameObject.activeInHierarchy) return false;
            result = ground.bounds;
            if (boundary == null) return result.size.x > 0 && result.size.z > 0;
            Vector3 min = result.min, max = result.max, center = boundary.transform.position;
            Vector2 half = Vector2.Max(Vector2.zero, boundary.PeacefulHalfSize - Vector2.one * BoundaryInset);
            min.x = Mathf.Max(min.x, center.x - half.x); max.x = Mathf.Min(max.x, center.x + half.x);
            min.z = Mathf.Max(min.z, center.z - half.y); max.z = Mathf.Min(max.z, center.z + half.y);
            if (max.x <= min.x || max.z <= min.z) return false;
            result.SetMinMax(min, max);
            return true;
        }
    }
}
