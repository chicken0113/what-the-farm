using System;
using UnityEditor;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class WeedBuilder
{
    [MenuItem("What The Farm/Create Weed Assets")]
    public static void Install()
    {
        const string prefabPath = "Assets/Resources/Weed.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing. Restore URP before creating weed assets.");
            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(.25f, .52f, .08f));
            AssetDatabase.CreateAsset(material, "Assets/Resources/WeedGreen.mat");
            var root = new GameObject("Weed");
            for (int i = -2; i <= 2; i++)
            {
                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = "Grass blade";
                blade.transform.SetParent(root.transform, false);
                blade.transform.localRotation = Quaternion.Euler(i * 9, i * 32, i * 12);
                blade.transform.localScale = new Vector3(.025f, .32f - Mathf.Abs(i) * .025f, .065f);
                blade.transform.localPosition = blade.transform.localRotation * Vector3.up * blade.transform.localScale.y * .5f;
                blade.GetComponent<Renderer>().sharedMaterial = material;
                UnityEngine.Object.DestroyImmediate(blade.GetComponent<Collider>());
            }
            var pickup = root.AddComponent<BoxCollider>(); pickup.center = new Vector3(0, .18f, 0); pickup.size = new Vector3(.32f, .36f, .3f);
            var body = root.AddComponent<Rigidbody>(); body.mass = .1f; body.isKinematic = true; body.useGravity = false;
            root.AddComponent<FarmItem>().Configure(ItemKind.Weed, 0, 3);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }
        const string configPath = "Assets/Resources/WeedSpawning.asset";
        if (AssetDatabase.LoadAssetAtPath<WeedSpawnSettings>(configPath) == null)
        {
            var config = ScriptableObject.CreateInstance<WeedSpawnSettings>(); config.prefab = prefab.GetComponent<FarmItem>();
            AssetDatabase.CreateAsset(config, configPath);
        }
        ItemPriceWindow.EnsureCatalog();
        AssetDatabase.SaveAssets();
    }
    public static void InstallAndValidate()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this check in an isolated batch project.");
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        Install();
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var root = new GameObject("Weed validation world");
        var config = UnityEngine.Object.Instantiate(Resources.Load<WeedSpawnSettings>("WeedSpawning"));
        Material soilMaterial = null;
        try
        {
            ground.transform.position = new Vector3(200, -.5f, 200); ground.transform.localScale = new Vector3(10, 1, 10);
            var soil = ground.AddComponent<SoilSurface>(); var world = root.AddComponent<FarmPrototype>();
            root.transform.position = new Vector3(200, 0, 200);
            config.initialCount = 0; config.intervalSeconds = 2; config.countPerInterval = 1; config.maxWildWeeds = 2;
            var spawner = root.AddComponent<WeedSpawner>(); spawner.Configure(world, config);
            Physics.SyncTransforms();
            spawner.Advance(1); Check(spawner.WildCount == 0, "Weed appeared before interval");
            spawner.Advance(1); Check(spawner.WildCount == 1, "Scheduled spawn failed");
            spawner.Advance(2); Check(spawner.WildCount == 2, "Second scheduled spawn failed");
            spawner.Advance(2); Check(spawner.WildCount == 2, "Wild weed cap ignored");
            var item = root.GetComponentInChildren<FarmItem>();
            Check(item.Kind == ItemKind.Weed && item.Value == ItemPriceCatalog.Active.Find("weed").salePrice, "Weed price or kind wrong");
            Vector3 originalScale = item.transform.lossyScale;
            int corner = 0;
            foreach (var wildItem in root.GetComponentsInChildren<FarmItem>())
                wildItem.transform.position = new Vector3(196 + corner++ * 8, .02f, 196);
            Physics.SyncTransforms();
            soilMaterial = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat"));
            var plot = soil.Till(new Vector3(200, 0, 200), 8, soilMaterial, soilMaterial);
            Check(spawner.CanSpawnAt(new Vector3(200, 0, 200), ground.GetComponent<Collider>()), "Tilled soil rejected for weeds");
            item.MarkHeld(); item.transform.SetParent(null, true);
            Check(spawner.WildCount == 1, "Collected weed still occupies wild cap");
            Check(spawner.TrySpawn(), "Wild weed did not spawn on tilled soil");
            var wildItems = root.GetComponentsInChildren<FarmItem>();
            Check(soil.FindPlot(wildItems[wildItems.Length - 1].transform.position) == plot, "Spawned weed is not on tilled soil");
            Check(world.TryPlant(item, plot, new Vector3(200, 0, 200)), "Weed could not be planted");
            plot.Water(100);
            Check(item.IsPlantedWeed && plot.IsOccupied && item.transform.lossyScale == originalScale && item.GetComponent<FleeingCrop>() == null,
                "Planted weed changed size or entered growth/combat system");
            item.MarkThrown(); item.GetComponent<Rigidbody>().isKinematic = false;
            Check(!item.ClaimSale(), "Planted weed sold");
            item.MarkHeld(); Check(!plot.IsOccupied && !item.IsPlantedWeed, "Picking weed did not release soil");
            var npc = root.AddComponent<NpcMerchant>(); npc.BindWorld(world);
            item.MarkThrown(); long before = world.Gold; int value = item.Value;
            Check(npc.TrySell(item) && world.Gold == before + value && !npc.TrySell(item), "Weed sale payout/duplicate protection failed");
            foreach (var remaining in root.GetComponentsInChildren<FarmItem>()) UnityEngine.Object.DestroyImmediate(remaining.gameObject);
            var boundary = root.AddComponent<FarmFirstStage>();
            ground.transform.localScale = new Vector3(100, 1, 100); Physics.SyncTransforms();
            config.maxWildWeeds = 30; spawner.Configure(world, config);
            var outside = root.transform.position + Vector3.right * 14;
            Check(!boundary.IsInsidePeacefulArea(outside) && !spawner.CanSpawnAt(outside, ground.GetComponent<Collider>()), "Weed allowed outside peaceful area");
            Check(!spawner.CanSpawnAt(root.transform.position + Vector3.right * 12.8f, ground.GetComponent<Collider>()), "Weed ignored boundary inset");
            for (int i = 0; i < 20; i++) Check(spawner.TrySpawn(), "Safe-area sampling failed on expanded map");
            foreach (var weed in root.GetComponentsInChildren<FarmItem>())
                Check(boundary.IsInsidePeacefulArea(weed.transform.position, .5f), "Spawned weed outside peaceful area");
            UnityEngine.Object.DestroyImmediate(boundary);
            Check(spawner.CanSpawnAt(outside, ground.GetComponent<Collider>()), "Later stages inherited first-stage boundary restriction");
            Debug.Log("WEED_SAFE_AREA_VALIDATION_SUCCESS: shifted stage origin, twenty spawns inside shared NPC boundary, inset and unrestricted later stage.");
            Debug.Log("WEED_VALIDATION_SUCCESS: timed random spawn, cap, tilled soil, pickup, unchanged planting, soil release and sale.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(ground);
            UnityEngine.Object.DestroyImmediate(config); if (soilMaterial != null) UnityEngine.Object.DestroyImmediate(soilMaterial);
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
