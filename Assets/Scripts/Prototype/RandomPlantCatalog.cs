using System;
using UnityEngine;

namespace WhatTheFarm.Prototype
{
    [CreateAssetMenu(menuName = "What The Farm/Random Plants")]
    public sealed class RandomPlantCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Plant
        {
            public string priceId;
            public string displayName;
            public GameObject model;
            [Min(.01f)] public float referenceSize = .6f;
        }
        public Plant[] plants = Array.Empty<Plant>();
        public Material material;
        public static RandomPlantCatalog Active => Resources.Load<RandomPlantCatalog>("RandomPlants");
        public Plant Pick()
        {
            var valid = Array.FindAll(plants, plant => plant != null && plant.model != null);
            return valid.Length == 0 ? null : valid[UnityEngine.Random.Range(0, valid.Length)];
        }
        public GameObject CreateModel(Plant plant, Vector3 position, Transform parent)
        {
            var root = new GameObject(plant.displayName);
            root.transform.SetPositionAndRotation(position, Quaternion.identity);
            root.transform.SetParent(parent, true);
            var visual = Instantiate(plant.model, root.transform, false);
            // Use just the highest-detail meshes; two LODs must not appear together in thumbnails.
            foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true))
            {
                var levels = lod.GetLODs();
                foreach (var renderer in lod.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                if (levels.Length > 0) foreach (var renderer in levels[0].renderers) if (renderer != null) renderer.enabled = true;
                lod.enabled = false;
            }
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            { collider.enabled = false; if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider); }
            var renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds BoundsOf()
            {
                var result = new Bounds(position, Vector3.zero); bool found = false;
                foreach (var renderer in renderers)
                    if (renderer.enabled) { if (!found) { result = renderer.bounds; found = true; } else result.Encapsulate(renderer.bounds); }
                return result;
            }
            var bounds = BoundsOf();
            visual.transform.localScale *= plant.referenceSize / Mathf.Max(.001f, bounds.size.x, bounds.size.y, bounds.size.z);
            bounds = BoundsOf();
            visual.transform.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = BoundsOf();
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (material != null || materials[i] != null) materials[i] = new Material(material != null ? material : materials[i]);
                renderer.sharedMaterials = materials;
            }
            var hitbox = root.AddComponent<BoxCollider>();
            hitbox.center = root.transform.InverseTransformPoint(bounds.center); hitbox.size = bounds.size;
            var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            foreach (var part in root.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 8;
            return root;
        }
    }
}
